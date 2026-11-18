using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Farm > Set Up > Suburb Residents
///
/// Moves the neighbours into the ten suburb houses: 18 people in ten households, each with a name, a personality (their
/// chat lines), a home and a weekly schedule that uses the park, the football field, the playground, the plaza benches,
/// the two shops and the road to the city.
///
///   House 1  the Abaras      Emeka (mail carrier), Grace (lectures at the university in the city), Nia (9, their daughter)
///   House 2  the Delgados    Walt and Rosa, a retired couple
///   House 3  the Nairs       Priya (a nurse, single mum) and Dev (16)
///   House 4  roommates       Bea (a botany student) and Tariq (a musician)
///   House 5  the Tanakas     Yuki (museum curator) and her father Kenji
///   House 6  the Bells       Marcus and Lena, a young couple
///   House 7  Sam             a writer who lives alone
///   House 8  the Reyes       Mateo (the park gardener) and his sister Lucia (football coach, works at city hall)
///   House 9  Ada             a florist who lives alone (with a cat you never see)
///   House 10 Gus             a retired builder, Frank's best friend, next door to Frank's store
///
/// WHERE THINGS ARE
///   - Pictures: Assets/Project/Art/Residents (npc_NAME.png = walk sheet, portraits_NAME.png = faces; same layout as the Kim family art).
///   - Schedules: Assets/Farm/Suburb/Residents/Schedules. An asset that already exists is KEPT, so your edits survive a rebuild.
///     Delete an asset to get its starter routes back.
///   - Chat lines and first-meeting lines are on each NPC in the scene (Scheduled Npc component) and are rewritten on every rebuild,
///     so change those in the table below, not in the scene.
///
/// HOW A DAY IS WRITTEN
///   Stay(name, start hour, priority, when, stops...)   the route ends at its last stop; the next route starts from there
///   Home(name, start hour, priority, when, stops...)   the route ends by walking home to bed
///   Stop(place, minutes, what they are doing)          place = an NpcSpot id (H1Kitchen, ParkBenchA, StoreCustomer ...)
///   City(minutes, what they are doing)                 walk off the right edge of the suburb and be away for that long
///   Two routes that overlap in time can't both run: the higher priority one wins and the other is DROPPED for that day.
///   Walking takes time (about 1.5 tiles per game minute; the park is 30 to 60 minutes from most houses), so leave gaps.
///   Farm > Fix and Diagnose > Check NPC Schedules prints everybody's week and warns about dropped routes.
///
/// Needs the houses (Farm > Set Up > Suburb Houses) in the scene. Farm > Set Up > Suburbs Scene runs everything in order.
/// Must live in a folder named "Editor".
/// </summary>
public static class SuburbResidentsMenu
{
    private const string Root = "Assets/Farm/Suburb/Residents";

    // ------------------------------------------------------------------ short names used in the table
    private const DayMask Mon = DayMask.Mon, Tue = DayMask.Tue, Wed = DayMask.Wed, Thu = DayMask.Thu, Fri = DayMask.Fri, Sat = DayMask.Sat, Sun = DayMask.Sun;
    private const DayMask Weekdays = Mon | Tue | Wed | Thu | Fri;
    private const WeatherMask DryWeather = WeatherMask.Clear | WeatherMask.Cloudy | WeatherMask.Fog;
    private const WeatherMask WetWeather = WeatherMask.Rain | WeatherMask.Storm | WeatherMask.Snow;

    private static NpcCondition Dry { get { return new NpcCondition { weather = DryWeather }; } }
    private static NpcCondition Wet { get { return new NpcCondition { weather = WetWeather }; } }
    private static NpcCondition On(DayMask days) { return new NpcCondition { days = days }; }
    private static NpcCondition DryOn(DayMask days) { return new NpcCondition { days = days, weather = DryWeather }; }

    private static ScheduleStop Stop(string place, float minutes, string doing)
    {
        return new ScheduleStop { place = place, waitMinutes = minutes, activity = doing };
    }

    /// <summary>
    /// A stop in the city: they walk to the right edge of the map, vanish, and come back 'minutes' (plus the trip) later.
    /// 'place' is an NpcSpot in the City scene (UniLectern, TowerOffice, HallDesk, PlantFloor...): a copy of the neighbour
    /// turns up there while they are away from the suburb (see CitySceneBuilder, "Visitors").
    /// </summary>
    private static ScheduleStop City(string place, float minutes, string doing)
    {
        return new ScheduleStop { place = place, scene = "City", waitMinutes = minutes, transitMinutes = 40f, activity = doing };
    }

    private static ScheduleRoute Home(string name, float hour, int priority, NpcCondition when, params ScheduleStop[] stops)
    {
        return new ScheduleRoute { routeName = name, startHour = hour, priority = priority, conditions = when ?? new NpcCondition(), stops = stops, whenFinished = RouteEnd.ReturnToIdleSpot };
    }

    private static ScheduleRoute Stay(string name, float hour, int priority, NpcCondition when, params ScheduleStop[] stops)
    {
        ScheduleRoute r = Home(name, hour, priority, when, stops);
        r.whenFinished = RouteEnd.StayAtLastStop;
        return r;
    }

    private static NpcChatLine Say(string line) { return new NpcChatLine { label = "Any time", priority = 0, conditions = new NpcCondition(), pages = new[] { line } }; }
    private static NpcChatLine Say(string label, int priority, NpcCondition when, string line) { return new NpcChatLine { label = label, priority = priority, conditions = when, pages = new[] { line } }; }

    // ------------------------------------------------------------------ the people
    private class Person
    {
        public string id, name, home;                // home = the house's spot prefix: "H1" ... "H10"
        public string bed = "Bed";                   // which bed spot they sleep at ("Bed" or "Bed2")
        public Vector2 offset;                       // stands this far from the exact spot (so couples don't stand inside each other)
        public Color color = new Color(0.3f, 0.4f, 0.5f);      // the colour of their name in the dialogue box
        public string[] hello;                       // said the first time you talk to them
        public NpcChatLine[] chat;
        public ScheduleRoute[] routes;
    }

    private static List<Person> People()
    {
        var all = new List<Person>();

        // ============================================================ HOUSE 1: the Abaras
        all.Add(new Person
        {
            id = "emeka", name = "Emeka", home = "H1", color = new Color(0.25f, 0.4f, 0.65f),
            hello = new[] { "Morning! Emeka Abara. I carry the mail, so I already know your name, your address and that nobody writes to you yet.", "We'll fix that. Everybody gets a postcard from me in their first week." },
            chat = new[]
            {
                Say("Eleven doorsteps every morning, rain or shine. Best job in the suburb: I get the news before the newspaper does."),
                Say("If you ever see my Nia chasing beetles on the football field, send her home for dinner, would you?"),
                Say("Frank orders so many catalogues I should charge him by the kilo."),
                Say("Rain", 10, Wet, "Wet letters, wet socks. The mail still goes out. That's the deal."),
                Say("Sunday", 12, On(Sun), "No mail on Sundays. Sundays are for football with the family."),
            },
            routes = new[]
            {
                Stay("Breakfast", 6.5f, 10, null, Stop("H1Kitchen", 30, "Having breakfast")),
                Stay("Morning mail round", 7.5f, 20, On(Weekdays | Sat),
                    Stop("H2Street", 4, "Delivering the mail"), Stop("H3Street", 4, "Delivering the mail"), Stop("StoreFront", 6, "Delivering to the Kims"), Stop("HwStreet", 6, "Delivering catalogues to Frank"),
                    Stop("H10Street", 4, "Delivering the mail"), Stop("H4Street", 4, "Delivering the mail"), Stop("H5Street", 4, "Delivering the mail"), Stop("H6Street", 4, "Delivering the mail"),
                    Stop("H9Street", 4, "Delivering the mail"), Stop("H8Street", 4, "Delivering the mail"), Stop("H7Street", 4, "Delivering the mail")),
                Stay("Lunch at home", 12.5f, 10, null, Stop("H1Kitchen", 40, "Having lunch")),
                Stay("Sunday football", 10f, 30, DryOn(Sun), Stop("FieldRight", 90, "Playing football with Nia"), Stop("ParkBenchB", 30, "Catching his breath")),
                Stay("Football with Nia", 16f, 10, Dry, Stop("FieldRight", 60, "Playing football with Nia"), Stop("ParkBenchB", 20, "Catching his breath")),
                Home("Evening", 19f, 10, null, Stop("H1Kitchen", 45, "Cooking dinner"), Stop("H1Living", 90, "Reading the paper")),
            }
        });
        all.Add(new Person
        {
            id = "grace", name = "Grace", home = "H1", offset = new Vector2(0.9f, 0f), color = new Color(0.7f, 0.3f, 0.3f),
            hello = new[] { "Good day. Dr. Grace Abara. I lecture in history at the university in the city, four days a week.", "And you are the new farmer. 'Farmer' comes from an old word for rent, did you know? No? You do now." },
            chat = new[]
            {
                Say("The city is forty minutes of walking and one very long road. I mark essays in my head on the way."),
                Say("Emeka knows everyone's business and tells nobody. It is the most admirable thing about him."),
                Say("A student wrote 'the olden days' in an essay this week. I had to sit down."),
                Say("Lecture days", 12, On(Mon | Tue | Wed | Thu), "I really must be going. Forty students are waiting to be disappointed by their grades."),
                Say("Rain", 10, Wet, "Rain. Excellent marking weather. Dreadful walking weather."),
            },
            routes = new[]
            {
                Stay("Breakfast", 6.25f, 10, null, Stop("H1Kitchen", 35, "Having breakfast")),
                Home("Lecturing in the city", 7.5f, 40, On(Mon | Tue | Wed | Thu), City("UniLectern", 420, "Teaching at the university")),
                Stay("Marking papers", 9f, 5, null, Stop("H1Living", 180, "Marking essays")),
                Stay("Sunday football", 10f, 30, DryOn(Sun), Stop("ParkBenchB", 120, "Cheering from the bench")),
                Stay("Shopping at the Kims'", 13.25f, 20, On(Fri | Sat), Stop("StoreCustomer", 15, "Buying groceries"), Stop("PlazaBench1", 40, "Chatting on the bench")),
                Home("Family dinner", 18.75f, 10, null, Stop("H1Kitchen", 60, "Having dinner"), Stop("H1Living", 60, "Reading")),
            }
        });
        all.Add(new Person
        {
            id = "nia", name = "Nia", home = "H1", bed = "Bed2", offset = new Vector2(-0.5f, -0.3f), color = new Color(0.85f, 0.5f, 0.25f),
            hello = new[] { "Hi! I'm Nia. I'm nine. Do you have bugs on your farm? What kind? How many legs?", "I'm going to be a bug scientist. Or a goalkeeper. Or both." },
            chat = new[]
            {
                Say("I found a beetle with green wings in the sandbox. Mr. Reyes says I have to let it go. I named it Gregory."),
                Say("Dad lets me win at football. I pretend I don't know."),
                Say("Why do you grow turnips? Does anybody actually like turnips?"),
                Say("School days", 8, On(Weekdays), "School is in the city. It takes FOREVER to get there."),
                Say("Rain", 10, Wet, "Worms come out when it rains! This is the best day."),
            },
            routes = new[]
            {
                Stay("Breakfast", 7f, 10, null, Stop("H1Kitchen", 30, "Having breakfast")),
                Home("School in the city", 8f, 40, On(Weekdays), City("UniDesk2", 330, "At school in the city")),
                Stay("Saturday at the playground", 9.5f, 20, DryOn(Sat), Stop("PlaySandbox", 60, "Digging for beetles"), Stop("PlaySeesaw", 30, "On the seesaw"), Stop("PlaySwings", 30, "On the swings")),
                Stay("Sunday football", 10f, 30, DryOn(Sun), Stop("FieldRight", 90, "Playing football with Dad"), Stop("ParkBenchB", 30, "Sharing an orange")),
                Stay("Lunch", 13.75f, 10, On(Sat | Sun), Stop("H1Kitchen", 30, "Having lunch")),
                Stay("Football, then the swings", 16.25f, 20, Dry, Stop("FieldRight", 50, "Playing football with Dad"), Stop("PlaySwings", 25, "On the swings")),
                Stay("Rainy afternoon", 16.25f, 10, Wet, Stop("H1Living", 120, "Drawing beetles")),
                Home("Dinner and bed", 19.5f, 10, null, Stop("H1Kitchen", 45, "Having dinner")),
            }
        });

        // ============================================================ HOUSE 2: the Delgados (retired couple)
        all.Add(new Person
        {
            id = "walt", name = "Walt", home = "H2", color = new Color(0.4f, 0.45f, 0.3f),
            hello = new[] { "Hm. You're the one who took the old farm. Walt Delgado.", "I built half the porches on this street, and the other half I had opinions about." },
            chat = new[]
            {
                Say("Kenji has beaten me at chess nine days running. I'm lulling him. It's a strategy."),
                Say("Frank sells a fine hammer and talks a load of nonsense. I go for the hammers."),
                Say("Rosa says I'm grumpy. I'm not grumpy. I'm accurate."),
                Say("Rain", 10, Wet, "My knee said rain two days ago. The knee is never wrong."),
                Say("Hardware days", 8, On(Tue | Thu | Sat), "Going up to Frank's later. He's got the wrong screws in the wrong bin again. Somebody has to tell him."),
            },
            routes = new[]
            {
                Stay("Coffee", 6.5f, 10, null, Stop("H2Kitchen", 40, "Drinking coffee")),
                Stay("Chess in the park", 8f, 10, Dry, Stop("ParkBenchA", 120, "Playing chess with Kenji")),
                Stay("Rainy morning", 8f, 20, Wet, Stop("H2Living", 150, "Doing the crossword")),
                Stay("Up to Frank's", 11.25f, 15, On(Tue | Thu | Sat), Stop("HwTools", 25, "Arguing with Frank about screws"), Stop("HwCustomer", 10, "Paying for one screw")),
                Stay("Lunch", 13.75f, 10, null, Stop("H2Kitchen", 40, "Having lunch")),
                Stay("Front garden", 15.5f, 10, Dry, Stop("H2Street", 75, "Weeding the front garden")),
                Home("Evening", 18f, 10, null, Stop("H2Kitchen", 60, "Having dinner"), Stop("H2Living", 120, "Watching the game")),
            }
        });
        all.Add(new Person
        {
            id = "rosa", name = "Rosa", home = "H2", offset = new Vector2(0.9f, 0f), color = new Color(0.75f, 0.35f, 0.5f),
            hello = new[] { "Oh, hello, dear! Rosa Delgado. You've met Walt? Don't mind him, he growls at everyone he likes.", "Do you eat bread? Of course you eat bread. I'll bring you a loaf." },
            chat = new[]
            {
                Say("I bake at six, the Kims have my bread by nine, and it's gone by ten. Mi-sook pays me in kimchi."),
                Say("The pigeons in the plaza know my footsteps. Walt says that's nothing to be proud of."),
                Say("That young Sam on Maple Lane never eats properly. I leave soup on the step. The bowl always comes back washed."),
                Say("Rain", 10, Wet, "A wet day is a soup day, dear. Remember that and you'll never be sad."),
                Say("Winter", 8, new NpcCondition { seasons = SeasonMask.Winter }, "I knit one scarf for every house on the street each winter. Yours is green."),
            },
            routes = new[]
            {
                Stay("Baking", 6.25f, 10, null, Stop("H2Kitchen", 90, "Baking bread")),
                Stay("Bread for the Kims", 8.5f, 10, null, Stop("StoreCustomer", 20, "Dropping off bread"), Stop("PlazaBench1", 45, "Feeding the pigeons")),
                Stay("Lunch", 12.5f, 10, null, Stop("H2Kitchen", 60, "Having lunch")),
                Stay("Playground bench", 16.25f, 10, Dry, Stop("ParkBenchD", 100, "Knitting and watching the children")),
                Home("Evening", 19.25f, 10, null, Stop("H2Kitchen", 50, "Having dinner"), Stop("H2Living", 100, "Knitting")),
            }
        });

        // ============================================================ HOUSE 3: the Nairs (single parent and a teenager)
        all.Add(new Person
        {
            id = "priya", name = "Priya", home = "H3", color = new Color(0.25f, 0.6f, 0.58f),
            hello = new[] { "Hi. Priya Nair. Sorry, I've been awake since four. I'm a nurse at the hospital in the city.", "If you cut yourself on a scythe, come to me before you go to Frank for duct tape." },
            chat = new[]
            {
                Say("Four early shifts a week. On my days off I do something wild, like sit down."),
                Say("Dev eats like a horse and trains like one. I'd complain, but he does the dishes."),
                Say("People think nurses want chocolates. We want shoes that don't hurt."),
                Say("Shift days", 12, On(Mon | Tue | Thu | Fri), "Early shift today. If I look asleep, I am. Keep talking, it's fine."),
                Say("Rain", 10, Wet, "Rainy days mean slips and sprains. Watch your step out there, please."),
            },
            routes = new[]
            {
                Home("Early shift at the hospital", 6.25f, 40, On(Mon | Tue | Thu | Fri), City("ClinicInside", 480, "Working at the hospital in the city")),
                Stay("Slow breakfast", 9f, 5, null, Stop("H3Kitchen", 45, "Having a slow breakfast")),
                Stay("Errands", 10.5f, 10, On(Wed | Sat), Stop("StoreCustomer", 20, "Buying groceries"), Stop("HwCustomer", 15, "Asking Frank about a leaky tap"), Stop("PlazaBench2", 30, "Sitting down, finally")),
                Stay("Watching Dev's practice", 17f, 10, Dry, Stop("ParkBenchA", 60, "Watching football practice")),
                Home("Evening", 19.25f, 10, null, Stop("H3Kitchen", 60, "Cooking dinner"), Stop("H3Living", 90, "Falling asleep on the sofa")),
            }
        });
        all.Add(new Person
        {
            id = "dev", name = "Dev", home = "H3", bed = "Bed2", offset = new Vector2(0.6f, -0.3f), color = new Color(0.8f, 0.3f, 0.25f),
            hello = new[] { "Oh. Hey. Dev. You're the farm person. Cool, I guess.", "Do you play football? We need a keeper. Ji-ho is terrible in goal, don't tell him I said so." },
            chat = new[]
            {
                Say("Coach Lucia made us run laps for being 'cheerful at the wrong time'. That's not even a rule."),
                Say("Blue slush is the best slush. Red is for people who've given up."),
                Say("Mum works crazy hours. So I cook on Thursdays. It's always pasta. Nobody complains."),
                Say("School days", 8, On(Weekdays), "School, then practice. That's the whole week. You get it."),
                Say("Rain", 10, Wet, "No practice in the rain. Coach says the pitch 'needs its rest'. It's grass."),
            },
            routes = new[]
            {
                Stay("Breakfast", 7f, 10, null, Stop("H3Kitchen", 20, "Wolfing down breakfast")),
                Home("School in the city", 7.75f, 40, On(Weekdays), City("UniDesk1", 350, "At school in the city")),
                Stay("Slush run", 11f, 15, On(Sat | Sun), Stop("StoreSlush", 20, "Choosing a slush"), Stop("StoreFront", 30, "Drinking a blue slush")),
                Stay("Football practice", 16f, 20, Dry, Stop("FieldLeft", 100, "At football practice"), Stop("ParkBenchA", 15, "Getting his breath back")),
                Stay("Rainy afternoon", 16f, 10, Wet, Stop("H3Living", 150, "Gaming")),
                Home("Dinner", 19.5f, 10, null, Stop("H3Kitchen", 45, "Having dinner"), Stop("H3Living", 90, "Gaming")),
            }
        });

        // ============================================================ HOUSE 4: roommates
        all.Add(new Person
        {
            id = "bea", name = "Bea", home = "H4", color = new Color(0.4f, 0.55f, 0.35f),
            hello = new[] { "Oh! Hello! Bea Lindqvist. I study botany at the university. You're a farmer, so you're basically my hero.", "Could I ask about your soil some time? I have... a list. A short list. Forty questions." },
            chat = new[]
            {
                Say("Soo-ah at the general store sorts her seed rack by season. It is the most beautiful thing in this town."),
                Say("My roommate Tariq practises guitar at midnight. I've started thinking of it as a very slow lullaby."),
                Say("Did you know a sunflower is actually thousands of tiny flowers? I think about that a lot."),
                Say("University days", 8, On(Mon | Wed | Fri), "Lab day. If I come home smelling of compost, it went well."),
                Say("Rain", 10, Wet, "Rain! Everything in the park is going to be so happy tomorrow."),
            },
            routes = new[]
            {
                Stay("Coffee and notes", 7f, 10, null, Stop("H4Kitchen", 45, "Having coffee over her notes")),
                Home("University", 8.25f, 40, On(Mon | Wed | Fri), City("UniLab", 360, "In the lab at the university")),
                Stay("Reading in the park", 10f, 10, DryOn(Tue | Thu | Sat | Sun), Stop("ParkBenchB", 120, "Reading under the trees")),
                Stay("Lunch", 13.5f, 10, null, Stop("H4Kitchen", 40, "Having lunch")),
                Stay("Seed shopping", 15.25f, 10, On(Tue | Thu), Stop("StoreSeeds", 25, "Reading every seed packet"), Stop("StoreCustomer", 10, "Buying just one packet")),
                Home("Evening", 18.75f, 10, null, Stop("H4Kitchen", 45, "Having dinner"), Stop("H4Living", 120, "Studying")),
            }
        });
        all.Add(new Person
        {
            id = "tariq", name = "Tariq", home = "H4", offset = new Vector2(-0.9f, 0f), color = new Color(0.45f, 0.35f, 0.6f),
            hello = new[] { "Hey, hey. Tariq. I'm the reason this street has a soundtrack.", "I play in the plaza most afternoons and in the city on weekends. Requests are free. Taste is extra." },
            chat = new[]
            {
                Say("Mornings are a rumour. I've never seen one."),
                Say("Gus sits through my whole set every day and says it's 'too modern'. He knows all the words."),
                Say("Bea labels the milk. And the shelf. And, once, me."),
                Say("Gig nights", 12, On(Fri | Sat), "Gig in the city tonight. Long walk with an amp. Worth it."),
                Say("Rain", 10, Wet, "Can't busk in the rain. The guitar sulks for a week."),
            },
            routes = new[]
            {
                Stay("Up at last", 10.5f, 10, null, Stop("H4Kitchen", 40, "Making very strong coffee")),
                Stay("Busking in the plaza", 12f, 10, Dry, Stop("PlazaBench2", 150, "Playing guitar")),
                Stay("Practising", 12f, 20, Wet, Stop("H4Living", 180, "Practising scales")),
                Stay("Slush break", 15.5f, 10, null, Stop("StoreSlush", 15, "Choosing a slush"), Stop("StoreFront", 20, "Drinking a slush")),
                Home("Gig in the city", 17.5f, 40, On(Fri | Sat), City("CafeInside", 240, "Playing a gig in the city")),
                Stay("Songwriting in the park", 17.5f, 10, Dry, Stop("ParkBenchC", 80, "Writing a song")),
                Home("Late dinner", 20.75f, 10, null, Stop("H4Kitchen", 40, "Having a late dinner"), Stop("H4Living", 100, "Playing quietly")),
            }
        });

        // ============================================================ HOUSE 5: the Tanakas (daughter and her father)
        all.Add(new Person
        {
            id = "yuki", name = "Yuki", home = "H5", color = new Color(0.3f, 0.35f, 0.55f),
            hello = new[] { "How do you do. Yuki Tanaka. I am a curator at the museum in the city.", "Your farm is older than this whole suburb, you know. If you dig up anything interesting, I would love to see it first." },
            chat = new[]
            {
                Say("The museum is closed on Mondays. So on Mondays, I visit other people's shops and judge their displays."),
                Say("My father does tai chi at dawn and wins at chess by nine. Retirement suits him far too well."),
                Say("Frank's sign is crooked by two degrees. I have not said anything. It is very hard."),
                Say("Work days", 8, On(Tue | Wed | Thu | Fri | Sat), "We are hanging a new exhibition this week. Everything is going wrong in a very orderly way."),
                Say("Rain", 10, Wet, "Rain is hard on old paper. I worry about the archive on days like this."),
            },
            routes = new[]
            {
                Stay("Tea", 6.75f, 10, null, Stop("H5Kitchen", 30, "Having tea")),
                Home("The museum in the city", 7.75f, 40, On(Tue | Wed | Thu | Fri | Sat), City("MuseumInside", 420, "Working at the museum")),
                Stay("Walk in the park", 9f, 10, DryOn(Sun | Mon), Stop("ParkBenchA", 60, "Watching her father play chess"), Stop("FieldSide", 20, "Taking the air")),
                Stay("Shopping", 11.75f, 10, On(Sun | Mon), Stop("StoreCustomer", 15, "Buying tea"), Stop("HwCustomer", 15, "Ordering picture hooks")),
                Home("Evening", 18.25f, 10, null, Stop("H5Kitchen", 60, "Cooking dinner"), Stop("H5Living", 90, "Reading")),
            }
        });
        all.Add(new Person
        {
            id = "kenji", name = "Kenji", home = "H5", bed = "Bed2", offset = new Vector2(-0.8f, 0f), color = new Color(0.45f, 0.5f, 0.55f),
            hello = new[] { "Ah. A new face. Kenji Tanaka. Sit, if you have time. Stand, if you do not.", "A farmer plants for people he will never meet. That is a good way to spend a life." },
            chat = new[]
            {
                Say("Walt thinks three moves ahead. I think about lunch. Somehow I keep winning."),
                Say("The slow river reaches the sea too. My daughter says this is not an excuse to be late."),
                Say("Every morning I greet the same crow on the football field. He has not greeted me back. Yet."),
                Say("Rain", 10, Wet, "Rain washes the board clean. Tomorrow we set the pieces again."),
                Say("Winter", 8, new NpcCondition { seasons = SeasonMask.Winter }, "In winter the tea is hotter and the chess is slower. Both are improvements."),
            },
            routes = new[]
            {
                Stay("Tai chi", 6.5f, 10, Dry, Stop("FieldSide", 60, "Doing tai chi")),
                Stay("Chess with Walt", 8.4f, 10, Dry, Stop("ParkBenchA", 130, "Playing chess with Walt")),
                Stay("Calligraphy", 7f, 20, Wet, Stop("H5Living", 180, "Practising calligraphy")),
                Stay("Lunch", 11.5f, 10, null, Stop("H5Kitchen", 60, "Having lunch")),
                Stay("Tea in the plaza", 15.75f, 10, Dry, Stop("PlazaBench1", 60, "Drinking tea from a flask")),
                Home("Evening", 18.25f, 10, null, Stop("H5Kitchen", 60, "Having dinner"), Stop("H5Living", 90, "Watching old films")),
            }
        });

        // ============================================================ HOUSE 6: the Bells (young couple)
        all.Add(new Person
        {
            id = "marcus", name = "Marcus", home = "H6", color = new Color(0.35f, 0.4f, 0.5f),
            hello = new[] { "Hey, good to meet you! Marcus Bell. I work in the city. At... the big tower. Yes, that tower.", "Don't tell Frank. Or the Kims. I buy everything from them, I promise. It's a guilt thing." },
            chat = new[]
            {
                Say("The tower sells everything Frank sells, cheaper, and nobody there knows what any of it is for."),
                Say("I jog round the football field every morning. Kenji and his crow are always there first."),
                Say("Lena draws me when I'm not looking. I look tired in all of them."),
                Say("Work days", 8, On(Weekdays), "Off to the tower. Forty floors, and my desk doesn't have a window. The farm must be nice."),
                Say("Weekend", 8, On(Sat | Sun), "Weekend! I'm fixing the porch. Frank sold me the paint and a lecture, both good."),
            },
            routes = new[]
            {
                Stay("Morning jog", 6.25f, 10, Dry, Stop("FieldSide", 3, "Jogging"), Stop("FieldRight", 3, "Jogging"), Stop("ParkBenchC", 3, "Jogging"), Stop("FieldLeft", 3, "Stretching")),
                Home("The tower in the city", 8f, 40, On(Weekdays), City("TowerOffice", 480, "Working at the tower in the city")),
                Stay("Buying local", 9.5f, 10, On(Sat | Sun), Stop("StoreCustomer", 15, "Buying groceries"), Stop("PlazaBench2", 40, "Having a coffee")),
                Stay("Paint from Frank's", 13f, 10, On(Sat), Stop("HwCustomer", 20, "Buying paint from Frank")),
                Stay("Park with Lena", 14f, 10, DryOn(Sun), Stop("ParkBenchB", 90, "Sitting in the sun with Lena")),
                Home("Evening", 19f, 10, null, Stop("H6Kitchen", 50, "Cooking dinner"), Stop("H6Living", 100, "Watching a film")),
            }
        });
        all.Add(new Person
        {
            id = "lena", name = "Lena", home = "H6", offset = new Vector2(0.9f, 0f), color = new Color(0.3f, 0.6f, 0.7f),
            hello = new[] { "Oh, hold still... no, sorry, go on. I'm Lena. I illustrate children's books, so I draw everything. Including you, just now.", "You have a very good hat face. Do you own a hat?" },
            chat = new[]
            {
                Say("I'm drawing every house on the street. Sam's has the best front door and the saddest curtains."),
                Say("Nia poses for me if I pay her in beetle facts. I mean, if I LISTEN to beetle facts."),
                Say("The light in the plaza around four o'clock is perfect. Don't tell the other illustrators. There aren't any, but still."),
                Say("Rain", 10, Wet, "Rain makes all the colours deeper. I just can't take the paper outside."),
                Say("Spring", 8, new NpcCondition { seasons = SeasonMask.Spring }, "Blossom season. I go through pink paint like you wouldn't believe."),
            },
            routes = new[]
            {
                Stay("Breakfast", 7.5f, 10, null, Stop("H6Kitchen", 40, "Having breakfast")),
                Stay("Sketching in the park", 9.5f, 10, Dry, Stop("ParkBenchB", 100, "Sketching the trees")),
                Stay("Drawing at home", 9.5f, 20, Wet, Stop("H6Living", 200, "Drawing")),
                Stay("Lunch", 13f, 10, On(Weekdays | Sat), Stop("H6Kitchen", 40, "Having lunch")),
                Stay("Park with Marcus", 14f, 20, DryOn(Sun), Stop("ParkBenchB", 90, "Sketching Marcus")),
                Stay("Plaza sketches", 15.25f, 10, DryOn(Mon | Wed | Fri), Stop("PlazaBench2", 75, "Sketching the shopfronts")),
                Stay("Groceries", 15.25f, 10, On(Tue | Sat), Stop("StoreCustomer", 15, "Buying groceries"), Stop("StoreFront", 15, "Sketching the store window")),
                Home("Evening", 19f, 10, null, Stop("H6Kitchen", 50, "Having dinner"), Stop("H6Living", 100, "Inking today's sketches")),
            }
        });

        // ============================================================ HOUSE 7: Sam (lives alone)
        all.Add(new Person
        {
            id = "sam", name = "Sam", home = "H7", color = new Color(0.6f, 0.45f, 0.25f),
            hello = new[] { "Oh. Um. Hello. Sam Whitaker. I write books. Well. A book. Most of one.", "Sorry, I'm not good at this part. The talking part." },
            chat = new[]
            {
                Say("I moved here for the quiet. Then I found out I can't write when it's quiet."),
                Say("Somebody leaves soup on my step every week. I've never caught them. I wash the bowl and it vanishes."),
                Say("The playground bench is the best seat in town. Everyone tells stories near a playground. I just... borrow them."),
                Say("Rain", 10, Wet, "Rain on the window. Good writing weather. I got four whole sentences."),
                Say("Plant days", 12, On(Tue | Thu | Sat), "I do three mornings a week at the treatment plant in the city. It pays for the noodles. And you would not BELIEVE what people throw away."),
                Say("Autumn", 8, new NpcCondition { seasons = SeasonMask.Autumn }, "Autumn is when the book is set. So this is research. Walking around is research."),
            },
            routes = new[]
            {
                Home("Shift at the treatment plant", 7.5f, 40, On(Tue | Thu | Sat), City("PlantFloor", 200, "Sorting waste at the treatment plant")),
                Stay("Writing", 8f, 5, null, Stop("H7Living", 180, "Writing")),
                Stay("Notebook at the playground", 11.5f, 10, Dry, Stop("ParkBenchD", 75, "Scribbling in a notebook")),
                Stay("Rain on the window", 11.5f, 20, Wet, Stop("H7Living", 150, "Writing, slowly")),
                Stay("Lunch", 14.25f, 10, null, Stop("H7Kitchen", 30, "Eating instant noodles")),
                Stay("Groceries", 16f, 10, On(Mon | Thu), Stop("StoreCustomer", 15, "Buying instant noodles")),
                Home("Evening", 19.75f, 10, null, Stop("H7Kitchen", 40, "Having dinner"), Stop("H7Living", 120, "Staring at the page")),
            }
        });

        // ============================================================ HOUSE 8: the Reyes (brother and sister)
        all.Add(new Person
        {
            id = "mateo", name = "Mateo", home = "H8", color = new Color(0.3f, 0.55f, 0.3f),
            hello = new[] { "Hello there. Mateo Reyes. I look after the park: the trees, the pitch, the sandbox, all of it.", "A farmer! Good. Somebody else who knows you can't hurry a plant." },
            chat = new[]
            {
                Say("People think the football field is just grass. It's eleven kinds of grass and a great deal of worry."),
                Say("Ada waters the plaza planters and I pretend not to check her work. She pretends not to check mine."),
                Say("My sister runs past me every morning at half past six and shouts 'faster!'. I'm raking."),
                Say("Rain", 10, Wet, "Rain does my watering for me. I'll be at home reading seed catalogues, like a holiday."),
                Say("Spring", 8, new NpcCondition { seasons = SeasonMask.Spring }, "The blossom trees are the one week a year when everybody thanks me."),
            },
            routes = new[]
            {
                Stay("Breakfast", 6.5f, 10, null, Stop("H8Kitchen", 30, "Having breakfast")),
                Stay("Tending the park", 7.5f, 20, DryOn(Weekdays | Sat),
                    Stop("PlaySandbox", 20, "Raking the sandbox"), Stop("ParkBenchD", 20, "Trimming the hedge"), Stop("ParkBenchA", 30, "Watering the trees"),
                    Stop("FieldSide", 40, "Marking the pitch lines"), Stop("ParkBenchB", 30, "Emptying the bins"), Stop("ParkBenchC", 30, "Weeding")),
                Stay("Seed catalogues", 8f, 20, Wet, Stop("H8Living", 180, "Reading seed catalogues")),
                Stay("Lunch on a bench", 12.25f, 10, DryOn(Weekdays | Sat), Stop("ParkBenchB", 40, "Eating a sandwich")),
                Stay("Plaza planters", 14.25f, 20, DryOn(Mon | Wed | Fri), Stop("PlazaPlanterMidE", 30, "Checking the planters"), Stop("PlazaPlanterE", 30, "Watering the planters")),
                Stay("Fertiliser from Frank's", 14.25f, 20, On(Tue | Thu), Stop("HwCustomer", 20, "Ordering fertiliser from Frank")),
                Stay("Sunday in the garden", 10f, 10, On(Sun), Stop("H8Street", 90, "Pottering in the front garden")),
                Stay("Watching the practice", 16.5f, 10, DryOn(Sun), Stop("ParkBenchA", 60, "Checking nobody ruins his pitch")),
                Home("Evening", 18.25f, 10, null, Stop("H8Kitchen", 50, "Cooking dinner"), Stop("H8Living", 100, "Dozing off")),
            }
        });
        all.Add(new Person
        {
            id = "lucia", name = "Lucia", home = "H8", offset = new Vector2(0.9f, 0f), color = new Color(0.85f, 0.55f, 0.15f),
            hello = new[] { "Hi! Lucia Reyes! I work at city hall in the mornings and coach the kids' football in the afternoons.", "You look strong. Farm strong. Ever played left back?" },
            chat = new[]
            {
                Say("Dev has the best left foot I've coached. If he finds out I said that, he'll stop running laps."),
                Say("City hall is forms, stamps and waiting. Coaching is shouting and oranges. Guess which one I'd do for free."),
                Say("My brother talks to the grass. The grass is immaculate, so I've stopped teasing him. Mostly."),
                Say("Rain", 10, Wet, "No practice in the rain! Mateo's rule. The pitch outranks the coach around here."),
                Say("City hall days", 8, On(Mon | Tue | Wed | Thu), "City hall this morning. If you ever need a permit, bring a sandwich. And a book."),
            },
            routes = new[]
            {
                Stay("Morning run", 6.5f, 10, Dry, Stop("PlaySlide", 2, "Running"), Stop("FieldLeft", 3, "Running laps"), Stop("FieldRight", 3, "Running laps")),
                Home("City hall", 8.25f, 40, On(Mon | Tue | Wed | Thu), City("HallDesk", 300, "Working at city hall")),
                Stay("Shopping", 11f, 10, On(Fri | Sat), Stop("StoreCustomer", 15, "Buying sports drinks"), Stop("PlazaBench1", 20, "Planning the weekend match")),
                Stay("Coaching football", 16.25f, 20, Dry, Stop("FieldMid", 90, "Coaching football")),
                Home("Evening", 19.5f, 10, null, Stop("H8Kitchen", 45, "Having dinner"), Stop("H8Living", 90, "Watching match replays")),
            }
        });

        // ============================================================ HOUSE 9: Ada (lives alone)
        all.Add(new Person
        {
            id = "ada", name = "Ada", home = "H9", color = new Color(0.5f, 0.4f, 0.65f),
            hello = new[] { "Well, hello, petal. Ada Kowalczyk. I had the flower stall in the city for thirty years. Now I just do the plaza planters, for love.", "Never water after sunset, never cut on a Friday, and never, ever tell a rose it looks tired." },
            chat = new[]
            {
                Say("The red tulips by the general store are sulking. Somebody's been sitting on the planter. I have my suspicions."),
                Say("My cat, Pan Tadeusz, doesn't go outside. He thinks outside is a rumour, same as that Tariq and mornings."),
                Say("Bea and I meet at the seed rack on Tuesdays. Lovely girl. Reads the back of every packet out loud."),
                Say("Rain", 10, Wet, "Rain on a Wednesday means a good week for sweet peas. My mother always said so."),
                Say("Spring", 8, new NpcCondition { seasons = SeasonMask.Spring }, "Spring! I've been awake since five. So have the daffodils. We don't like to waste it."),
            },
            routes = new[]
            {
                Stay("Tea with the cat", 6.75f, 10, null, Stop("H9Kitchen", 40, "Having tea with the cat")),
                Stay("Plaza flowers", 8f, 10, Dry, Stop("PlazaPlanterW", 35, "Watering the planters"), Stop("PlazaPlanterMidW", 35, "Talking to the tulips"), Stop("PlazaPlanterMidE", 35, "Watering the planters"), Stop("PlazaPlanterE", 35, "Deadheading")),
                Stay("Repotting", 8f, 20, Wet, Stop("H9Living", 180, "Repotting on the living room floor")),
                Stay("Lunch", 12.75f, 10, null, Stop("H9Kitchen", 40, "Having lunch")),
                Stay("Seeds and gossip", 15.25f, 10, On(Tue | Thu | Sat), Stop("StoreSeeds", 25, "Choosing seeds"), Stop("StoreCustomer", 10, "Catching up on the news")),
                Stay("Front garden", 15.25f, 5, Dry, Stop("H9Street", 60, "Deadheading the roses")),
                Home("Evening", 18.5f, 10, null, Stop("H9Kitchen", 50, "Having dinner"), Stop("H9Living", 120, "Pressing flowers")),
            }
        });

        // ============================================================ HOUSE 10: Gus (lives alone, next door to Frank's)
        all.Add(new Person
        {
            id = "gus", name = "Gus", home = "H10", color = new Color(0.65f, 0.3f, 0.25f),
            hello = new[] { "Gus Petrakis. Forty years a builder. I put the roof on Frank's store, and I've been keeping an eye on it since.", "You need anything built, don't ask me, I'm retired. Ask me how I WOULD have built it. That's free." },
            chat = new[]
            {
                Say("Frank's prices are robbery. I tell him every morning. Then I buy a box of nails so he doesn't feel bad."),
                Say("Walt Delgado puts his fence posts in crooked. I go down there most afternoons to say so. It's exercise."),
                Say("That boy with the guitar plays the same four songs. I've nearly learned the words. Don't tell him."),
                Say("Rain", 10, Wet, "Rain. Good. We'll find out whose roof I did and whose roof somebody else did."),
                Say("Sunday", 12, On(Sun), "Sundays Frank shuts the store and we watch the football in the park. He brings sandwiches. They're terrible."),
            },
            routes = new[]
            {
                Stay("Coffee", 7f, 10, null, Stop("H10Kitchen", 40, "Drinking coffee")),
                Stay("Supervising Frank", 8.25f, 10, On(Weekdays | Sat), Stop("HwCustomer", 60, "Telling Frank how to run his store"), Stop("HwTools", 30, "Just looking")),
                Stay("Sunday football", 10f, 30, On(Sun), Stop("ParkBenchB", 110, "Watching the football with Frank")),
                Stay("Paper on the bench", 11.5f, 10, DryOn(Weekdays | Sat), Stop("PlazaBench2", 60, "Reading the paper")),
                Stay("Lunch", 13.25f, 10, null, Stop("H10Kitchen", 40, "Having lunch")),
                Stay("Heckling Walt", 15.25f, 10, Dry, Stop("H2Street", 40, "Telling Walt his fence is crooked")),
                Stay("Dinner", 17.25f, 10, null, Stop("H10Kitchen", 30, "Having dinner")),
                Home("Evening on the bench", 19f, 10, DryOn(Weekdays | Sat), Stop("PlazaBench2", 60, "Putting the world right with Frank")),
            }
        });
        return all;
    }

    // ------------------------------------------------------------------ menu
    [MenuItem("Farm/Set Up/Suburb Residents", false, 152)]
    public static void BuildFromMenu()
    {
        string result = Build();
        EditorUtility.DisplayDialog(result.StartsWith("Placed") ? "Residents moved in" : "Suburb residents", result, "OK");
    }

    /// <summary>Places every resident in the open scene. Returns a sentence saying what happened (it starts with "Placed" when it worked). No pop-ups.</summary>
    public static string Build()
    {
        string art = HardwareEditorUtil.FindFolder("npc_emeka");
        if (art == null) return "The residents' art (npc_emeka.png and friends) was not found. It belongs in Assets/Project/Art/Residents.";
        if (HardwareEditorUtil.FindSpot("H1Bed") == null) return "No suburb houses found in this scene (no 'H1Bed' spot). Run Farm > Set Up > Suburb Houses first.";
        if (GameObject.Find("Residents") != null) return "The residents are already in this scene. Delete the 'Residents' object to place them again (your schedule assets are kept).";

        // cut the sheets: walk sheets are 16 x 28 cells, portrait sheets are four 64 x 64 faces (sprite IDs stay the same every run: see StableSpriteId)
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { art }))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            string n = Path.GetFileNameWithoutExtension(p);
            if (n.StartsWith("npc_")) HardwareEditorUtil.SliceSheet(p, 16, 28, SpriteAlignment.BottomCenter);
            else if (n.StartsWith("portraits_")) HardwareEditorUtil.SliceSheet(p, 64, 64, SpriteAlignment.Center);
        }
        AssetDatabase.Refresh();
        HardwareEditorUtil.EnsureFolder(Root + "/Schedules");

        var parent = new GameObject("Residents");
        Undo.RegisterCreatedObjectUndo(parent, "Suburb residents");
        int placed = 0;
        var missing = new List<string>();
        foreach (Person p in People())
        {
            string idle = p.home + p.bed;
            NpcSpot spot = HardwareEditorUtil.FindSpot(idle);
            if (spot == null) { missing.Add(p.name + " (no spot '" + idle + "')"); continue; }

            var go = new GameObject("Npc_" + p.name);
            go.transform.SetParent(parent.transform, false);
            go.transform.position = spot.transform.position + (Vector3)p.offset;
            go.AddComponent<SpriteRenderer>().sortingOrder = 0;
            var npc = go.AddComponent<ScheduledNpc>();
            npc.displayName = p.name;
            npc.schedule = MakeSchedule(p, idle);
            npc.standOffset = p.offset;
            npc.nameColor = p.color;
            npc.firstMeeting = p.hello;
            npc.chatLines = p.chat;
            HardwareEditorUtil.GiveNpcArt(npc, art, "npc_" + p.id, "portraits_" + p.id);
            if (npc.down == null || npc.down.Length == 0) Debug.LogWarning("Suburb residents: no walk pictures found for " + p.name + " (npc_" + p.id + ".png).");
            EditorUtility.SetDirty(npc);
            placed++;
        }
        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Selection.activeGameObject = parent;
        return "Placed " + placed + " residents under 'Residents', each with a schedule in " + Root + "/Schedules." +
               (missing.Count > 0 ? "\n\nLeft out, because their home is not in this scene: " + string.Join(", ", missing) + "." : "") +
               "\n\nFarm > Fix and Diagnose > Check NPC Schedules prints everybody's week.";
    }

    private static NpcScheduleData MakeSchedule(Person p, string idleSpot)
    {
        string path = Root + "/Schedules/Schedule_" + p.id + ".asset";
        var s = AssetDatabase.LoadAssetAtPath<NpcScheduleData>(path);
        if (s != null) return s;                    // keep the developer's own edits
        s = ScriptableObject.CreateInstance<NpcScheduleData>();
        s.npcId = p.id;
        s.idleSpot = idleSpot;
        s.routes = p.routes;
        AssetDatabase.CreateAsset(s, path);
        return s;
    }
}
