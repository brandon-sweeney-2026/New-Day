# Suburb Story Arcs

A 10-part storyline for each of the 23 suburb NPCs, plus the choices you have for how arcs are built.

**Chosen: Option C (branching).** The system is built (`NpcStoryArc`, `StoryArcs`, `Farm > Set Up > Story Arcs`). **All 23 arcs are written out in full** in `Assets/Project/Editor/StoryArcSetupMenu.cs`. This document is the original plan; where they differ, the code is the real version. Some beats here (schedule changes, mail, plaza events, scene moments) were simplified to dialogue in the built versions.

---

## 1. How the 10 parts work

The friendship system already has **10 hearts** (`PlayerMenuSettings.maxHearts = 10`, 100 points per heart), and points are stored as the counter `friend_<npcId>`. So the simplest rule is:

> **Part N unlocks at N hearts.**

A chat line with the condition "counter `friend_walt` at least 400" is Walt's Part 4. That works with the code you have today, with no new scripts.

### Your options for structure

| Option | How it works | Good | Bad |
|---|---|---|---|
| **A. Hearts only** | Part N at N hearts, nothing else | Works today, easy to test | Every arc feels the same; the world never reacts |
| **B. Hearts + world gates** *(recommended)* | Most parts need only hearts; 2-3 parts per NPC also need a flag, season, counter or another NPC's progress (e.g. "Marcus Part 7 needs `saw_tower_dumping`") | The suburb reacts to what you do in the city, river and mine; stories cross over | Needs a little care so nobody gets stuck |
| **C. Branching** | Option B, plus one **choice** around Part 8 that picks one of two endings for Parts 9-10 | Replay value, real stakes against Sterling | Needs a new dialogue-choice feature (see section 6); twice the writing for Parts 9-10 |
| **D. Ensemble chapters** | Arcs are grouped into 7 shared storylines (section 3) that advance together | Feels like a TV season; the town changes together | Hardest to schedule; one stuck NPC blocks a whole chapter |

**Recommendation:** build **B** first for everyone (it needs no new code), and write every arc so a **C** choice can be dropped in at Part 8 later. Each arc below marks its fork with ⚑.

### Your options for tone

- **Cozy slice-of-life**: problems are small (a fence, a book, a recipe). Sterling is background.
- **Cozy with teeth** *(recommended, and what's written below)*: most arcs are personal, but about a third of each arc touches Sterling, the river, or the city. The suburb is the front line of the main plot.
- **Main-plot heavy**: every arc is a piece of the case against Sterling. Strong story, but the cast starts to feel like tools.

### Your options for delivering a part

1. **Chat line**: a multi-page line gated by hearts and flags. Works today.
2. **Schedule change**: a new route gated by a flag (Walt starts walking to Gus's house). Works today, because routes use the same `NpcCondition`.
3. **Mail**: Emeka delivers a letter to your mailbox. The mailbox exists; triggering a letter from a story flag may need a small hook.
4. **Quest**: `QuestLog.Add` / `Complete`, like `FairyNpc`. Needs a small script per quest or one general "story quest" component.
5. **Scene moment**: two NPCs meet at a spot and talk while you watch. Needs a small "cutscene" feature.

---

## 2. The big picture: what the suburb is about

The suburb is a place where everyone knows your name, next to a city where Victor Sterling's tower sells everything cheaper and dumps waste in the river. Every arc is a small version of one question:

> **Do people stay small, local and connected, or do they give in to Sterling's bigger, cheaper, lonelier way?**

The player doesn't fix everyone. Some NPCs drift toward Sterling and come back, some don't. With option C, the player's Part 8 choices decide who.

**Sterling's reach in the suburb** (shows up across many arcs):
- **The tower** undercuts Frank, the Kims and Ada's old flower stall.
- **Sterling money** funds the university (Grace, Bea), a private clinic (Priya), the museum (Yuki) and a football academy (Dev).
- **Sterling jobs**: Marcus already works there; Emeka, Rosa's son, Soo-ah and Tariq all get offers.
- **Sterling waste** reaches the river (Nia's bugs, Sam at the treatment plant, Priya's patients, Mateo's park).

---

## 3. Shared storylines (crossovers)

These seven threads run through several arcs. They're what make the suburb feel like one town.

| Thread | Who | What happens |
|---|---|---|
| **The Soup Mystery** | Rosa, Sam, (Emeka) | Someone leaves soup on Sam's step. You find out it's Rosa, keep her secret, and eventually bring them together. |
| **The Old Partnership** | Walt, Gus, Kenji, Frank | Walt and Gus were once "Delgado & Petrakis Builders". Their fence feud is an old wound. Kenji is the quiet referee. |
| **The Tower File** | Marcus, Sam, Grace, Bea, Priya, Lucia | Each holds one piece of proof about Sterling's dumping: the manifests, the waste, the history, the soil, the patients, the permits. |
| **The Suburb Cup** | Lucia, Dev, Ji-ho, Nia, Emeka, Mateo | A football tournament against a city team (sponsored by Sterling). Practice, a scout, the final on Mateo's pitch. |
| **The Bloom Show** | Ada, Bea, Soo-ah, Mateo, Rosa | A spring flower and produce show in the plaza. Your crop quality matters here. |
| **The Street Book** | Lena, Sam, Tariq, Emeka | Lena draws every house, Sam writes the words, Tariq writes a song, Emeka knows everyone's story. |
| **The Farm's Past** | Yuki, Grace, Kenji, Walt | Your farm is older than the suburb. Things you dig up reveal how the valley looked before the tower. |

---

## 4. The arcs

Each arc has: the **core** (what the character wants vs. what's in the way), **10 parts**, the **fork** ⚑ at Part 8, and **other directions** you could take instead.

Gate tags: `[hearts only]`, `[+flag ...]`, `[+season ...]`, `[+NPC part]` (another NPC must be at a part), `[quest]`, `[mail]`, `[route]`.

---

### THE KIMS (General Store and apartment)

#### Jae-hoon Kim, shopkeeper
**Core:** Thirty years behind the till. He wants the store to outlive him. The tower's prices and his Sunday wholesaler are squeezing him.

1. He shows you the first dollar the store ever took, framed under the till. `[hearts only]`
2. He asks what you grow. He's thinking of selling local produce. `[hearts only]`
3. Quest: bring him 10 of one crop. He sells it under a hand-written "From the farm" sign. `[quest]`
4. His wholesaler in the city raises prices. The wholesaler was bought by Sterling. `[+flag research or city visited]`
5. Late at night you find him doing sums at the kitchen table. The store is losing money. `[route: AptDining at 22:00]`
6. He admits he never asked Ji-ho if he *wants* the store. `[+Ji-ho Part 5]`
7. A Sterling rep (Marcus, awkwardly) brings an offer: become a "Sterling Express" franchise. `[+Marcus Part 4]`
8. ⚑ **Fork:** help him become the town's local supplier (you ship him crops every week), or tell him to take the franchise.
9. **Local:** the "From the farm" shelf sells out every Saturday. **Franchise:** the store gets bright and cold, and regulars stop chatting at the till.
10. **Local:** he hands Ji-ho the keys for one day a week and frames a second dollar, from your first delivery. **Franchise:** he tears down the Sterling sign himself and says "Thirty-one years. Starting again."

**Other directions:** (a) a health scare, so the family has to run the store without him; (b) a rival corner shop opens and turns out to be a friendly old classmate.

#### Mi-sook Kim, feeds everyone
**Core:** She takes care of the whole street and never herself. Her "Saturday friends in the city" are really a cooking class she is too shy to mention.

1. She refuses to let you leave without a rice ball. `[hearts only]`
2. Recipe swap with Rosa: who won? Each tells you a different story. `[hearts only]`
3. She asks for a specific crop "for something". `[quest]`
4. You see her in the City café with a cooking-class apron. She makes you promise not to tell. `[+City scene, Saturday]`
5. Her teacher says she should enter a city cooking competition. She says no. `[hearts only]`
6. Soo-ah finds out and is hurt she wasn't told. `[+Soo-ah Part 4]`
7. Quest: gather Gold-quality ingredients for her competition dish. `[quest, crop quality]`
8. ⚑ **Fork:** encourage her to enter the Sterling-sponsored competition (big prize, a TV spot), or to cook a "street dinner" for the whole suburb instead.
9. **Competition:** she's second, and her dish gets put on a Sterling ready-meal without credit. **Street dinner:** long tables in the plaza, everyone brings one thing.
10. Either way she finally eats *first* at a meal, and someone else cooks for her: you. Unlocks her recipe for crafting.

**Other directions:** (a) she wants to visit Korea, and the family has to manage without her for a season; (b) a cookbook of the whole street's recipes (connects to Lena).

#### Ji-ho Kim, the reluctant heir
**Core:** Everyone thinks he's lazy. He's actually great with numbers and people, he just doesn't want *his parents'* life.

1. "If Mom asks, I already restocked the coolers." He hasn't. `[hearts only]`
2. He wants a games corner in the store. You see him sketch a floor plan. `[hearts only]`
3. Five-a-side with Dev. He's a terrible keeper and knows it. `[+Suburb Cup start]`
4. He reorganises the slush machine prices and sales go up. He tells nobody. `[hearts only]`
5. He confesses he's been accepted to a business course in the city and hasn't told his parents. `[hearts only]`
6. He works out exactly how much the tower is costing the store, on a napkin. `[+Jae-hoon Part 5]`
7. He plays in the Suburb Cup, in goal, because nobody else will. `[+Lucia Part 6]`
8. ⚑ **Fork:** tell him to take the course, or to stay and modernise the store his way.
9. **Course:** he comes home on weekends with ideas. **Stay:** the games corner opens, and teens hang out at the store instead of the tower.
10. He and his dad stand at the till together. Jae-hoon pretends not to be proud and fails.

**Other directions:** (a) esports dream: he enters a city tournament sponsored by Sterling; (b) he and Dev start a delivery service with a bike.

#### Soo-ah Kim, the seed rack
**Core:** A plant nerd studying in the city. She wants to save old, local seed varieties before Sterling's patented seeds replace them.

1. "I sorted the seed rack by season AND colour. You noticed, right?" `[hearts only]`
2. She asks you to tell her when a seed packet looks faded. `[hearts only]`
3. She shows you a tin of very old seeds found in the store's basement. `[hearts only]`
4. Quest: grow one of the old seeds. It's a variety nobody has seen in decades. `[quest, +season]`
5. Bea comes to the seed rack and the two of them talk for three hours. `[+Bea Part 4]` (optional romance)
6. Her professor offers her a paid internship at Sterling AgriSeed. `[hearts only]`
7. She learns AgriSeed seeds are bred so you can't save seeds from the crop. `[+flag research_<seeds>]`
8. ⚑ **Fork:** take the internship (and learn their secrets), or start a free **seed library** in the store.
9. **Internship:** she brings back proof AgriSeed seeds fail in polluted soil. **Library:** neighbours borrow seeds and bring back more.
10. A shelf of local heirloom seeds goes on sale at the store with *your farm's name* as the grower. (New seed items unlock.)

**Other directions:** (a) she and Bea become a couple (a plain romance arc); (b) she wants to farm herself and asks to rent a corner of your land.

---

### FRANK (Frank's Home and Hardware)

#### Frank
**Core:** Loud, warm, and a dreamer. The cot in his office isn't for naps: he sold his house to keep the store open. Nobody knows, not even Gus.

1. "Measure twice, cut once." He gives you a free box of nails. `[hearts only]`
2. He tells the story of every item on one shelf, far too long. `[hearts only]`
3. You notice the cot, the hammer pillow and a toothbrush in the staff office. `[route: Frank in office at night]`
4. Sterling's "doors in a box" come out at half his price. `[+house_upgrades ≥ 3]`
5. Gus finds out about the house. They have their first real argument. `[+Gus Part 5]`
6. He asks you to quietly test-buy at the tower and report back. `[+City scene]`
7. The bank sends a final letter. Emeka delivers it with a face like a funeral. `[mail]`
8. ⚑ **Fork:** help him turn the store into a **community co-op** (neighbours buy shares, you pay the most), or help him negotiate a sale to Sterling for a good price.
9. **Co-op:** Walt, Gus, Marcus and the Kims each take a share; Saturday workshops start. **Sale:** he gets money but sits on the plaza bench all day with nothing to fix.
10. **Co-op:** you use house upgrades to build Frank a little home (a new building next to the store). **Sale:** he opens a tiny repair stall in the plaza and is happier than ever.

**Other directions:** (a) the Gus/Frank friendship is the whole arc (what they've meant to each other for forty years); (b) Frank's lost love from his youth comes back to town as a Sterling buyer.

---

### HOUSE 1: The Abaras

#### Emeka Abara, the mail carrier
**Core:** He knows everyone's business and tells nobody. Sterling wants to replace his route with parcel lockers.

1. Your first postcard arrives, from Emeka himself. `[mail]`
2. He asks you to deliver a parcel he forgot to Gus. `[quest]`
3. He shows you the "dead letter box": twenty years of mail nobody could deliver. `[hearts only]`
4. Sterling parcel lockers go up in the plaza. His route is cut to three days a week. `[+day ≥ 30 or flag]`
5. Nia asks you why Dad is sad. `[+Nia Part 4]`
6. Quest: deliver five dead letters around the street. Each one opens a small moment with another NPC. `[quest]`
7. One dead letter is addressed to your farm, to the previous owner. You read it. (Hook for the farm's past.) `[quest]`
8. ⚑ **Fork:** help him petition city hall (Lucia) to keep the route, or encourage him to take Sterling's "logistics supervisor" job.
9. **Petition:** the whole street signs; the lockers stay empty. **Job:** he's at the tower, the street gets no postcards, and nobody knows the news.
10. A letter arrives for you, from everyone on the street. (Or, after the job ending, Emeka quits and walks the old route one more time.)

**Other directions:** (a) the oath arc: he finds a letter that proves Sterling's dumping and must decide whether to break his rule of never reading mail; (b) retirement: he trains Dev as a summer mail carrier.

#### Dr. Grace Abara, historian
**Core:** She's writing the history of the valley and finds out the Sterling family drained the old wetland the river used to feed. Her university is funded by Sterling.

1. "'Farmer' comes from an old word for rent." She lends you a book. `[hearts only]`
2. She asks you to bring anything old you dig up. `[hearts only]`
3. Quest: bring an artifact from tilling or the mine. `[quest]`
4. The artifact is a survey marker from before the suburb existed. `[+Yuki Part 3]`
5. The university opens the "Sterling Wing". Her funding is now Sterling money. `[hearts only]`
6. She finds old maps: Sterling's grandfather drained the wetland, and the river has been sick ever since. `[+flag river or research]`
7. The dean tells her to drop the topic. She marks essays angrily at the kitchen table. `[route]`
8. ⚑ **Fork:** help her publish (and risk her job), or keep the files safe and wait.
9. **Publish:** a public lecture in the plaza; the whole street comes, and Victor hears. **Wait:** she gives you the files "for when the time is right". (Evidence item.)
10. A history plaque goes up in the plaza. Or, quietly, she adds a chapter to Lena's street book.

**Other directions:** (a) family: she's always marking and Nia is growing up without her noticing; (b) academic rivalry with a smug city historian who is writing Sterling's official history.

#### Nia Abara, age 9, future bug scientist
**Core:** Bugs, football and endless questions. She's the first to notice that the river is making the bugs sick.

1. "Do you have bugs on your farm? How many legs?" `[hearts only]`
2. She shows you Gregory the beetle (in a jar, briefly). `[hearts only]`
3. Quest: bring her three different insects or forage finds. `[quest]`
4. She finds a sick, grey beetle near the river. `[+RiverPollution.Level ≥ 2]`
5. She decides to do a science-fair project on the river. Bea helps. `[+Bea Part 5]`
6. She asks you to take her to the farm's top rows, where the river runs. (She walks a route to the Farm, if supported.) `[route]`
7. Lucia wants her in goal for the Suburb Cup final, on the same day as the science fair. `[+Lucia Part 7]`
8. ⚑ **Fork:** you help her choose the cup or the fair. (There's no wrong choice. Her parents back either one.)
9. **Fair:** she wins and the city newspaper prints "9-year-old finds poison in river". **Cup:** she saves a penalty, and Dev carries her on his shoulders.
10. She names a new beetle after you. The other ending finds its way back: Bea enters her project anyway, or Lucia gives her an honorary medal.

**Other directions:** (a) friendship with Kenji's crow (she's determined to make it say hello); (b) she's scared of the dark, and you help her be brave on a mine trip.

---

### HOUSE 2: The Delgados

#### Walt Delgado, grumpy carpenter
**Core:** Proud, and his knee is failing. He and Gus were partners until a job went wrong thirty years ago.

1. "I'm lulling Kenji. It's a strategy." `[hearts only]`
2. He critiques your fence. Then he fixes it while you're out. `[hearts only]`
3. He asks for hardwood. He's building "something", and won't say what. `[quest]`
4. His knee gives way in the front garden. You find him and fetch Priya. `[+Priya Part 3]`
5. Rosa tells you he won't see a doctor. `[+Rosa Part 4]`
6. He shows you an old photo: "Delgado & Petrakis Builders". `[hearts only]`
7. The truth: Gus took a Sterling contract back then, and the building it was for was the first tower. Walt walked out. `[+Gus Part 6]`
8. ⚑ **Fork:** get Walt and Gus to build something together (a plaza stage, a gazebo, a bandstand), or help Walt finish his secret project alone.
9. **Together:** they argue the whole time and build the best thing on the street. **Alone:** it's a cradle, for a grandchild he hasn't told Rosa about wanting.
10. He finally beats Kenji at chess. Or he lets Kenji win, and you catch him doing it.

**Other directions:** (a) mentor: Walt teaches Dev carpentry, and Dev turns out to be good; (b) Sterling offers to buy the Delgado house for the new road.

#### Rosa Delgado, baker
**Core:** The street's grandmother. She leaves soup on Sam's step because Sam reminds her of their son Tomás, who works in the tower and doesn't visit.

1. She brings you a loaf. "Of course you eat bread." `[hearts only]`
2. The recipe swap with Mi-sook, from her side. `[hearts only]`
3. You catch her leaving soup on Sam's step at 6 a.m. `[route: early, Sam's street]`
4. She asks you not to tell Sam. `[hearts only]`
5. She tells you about Tomás, in the city, in the tower. `[hearts only]`
6. Quest: grow wheat (or her chosen crop) for a festival bread. Quality matters. `[quest, crop quality]`
7. The tower opens a bakery counter. The Kims' bread sales fall. `[+Jae-hoon Part 4]`
8. ⚑ **Fork:** help Rosa start a Saturday **market** in the plaza, or suggest she sell her bread to the tower bakery.
9. **Market:** stalls from Ada, Soo-ah, Mi-sook and you. **Tower:** her bread is in plastic with a Sterling logo, and she stops baking at home.
10. Tomás visits, finally, and the whole street meets him. (Needs a new NPC, or a visit that happens off-screen.) She knits you a second scarf.

**Other directions:** (a) a gentle grief arc instead (Tomás passed away years ago; soup is how she copes); (b) the soup mystery is the whole arc, and you play matchmaker for her and Sam as a "grandmother and grandchild" pair.

---

### HOUSE 3: The Nairs

#### Priya Nair, nurse and single mum
**Core:** Exhausted. The city hospital is understaffed, and a shiny Sterling clinic offers her double the pay. She's also seeing more and more patients with river sickness.

1. "Come to me before you go to Frank for duct tape." `[hearts only]`
2. She asks for herbal tea ingredients (a forage item). `[quest]`
3. She falls asleep on the plaza bench. You make sure Dev gets dinner. `[hearts only]`
4. She treats Walt's knee and gets him to listen, barely. `[+Walt Part 4]`
5. More patients from the river side of the city. Rashes, coughs. `[+RiverPollution.Level ≥ 3]`
6. Dev's dad sends a birthday card a month late. She's angry, then tired. `[mail, hearts only]`
7. The Sterling clinic offers her a job: better hours, more pay, and a clause saying "no talking to the press". `[hearts only]`
8. ⚑ **Fork:** take the clinic job (and be gagged), or stay and give you her patient records (anonymous) for the Tower File.
9. **Clinic:** she sleeps more, smiles less. **Stay:** she's tired but proud, and you've got a key piece of evidence.
10. A "Community Health Day" in the plaza, with your herb garden supplying tea. Dev cooks pasta for everyone.

**Other directions:** (a) romance: a new doctor at the hospital, and Dev is mortified; (b) she trains you in first aid, which unlocks a healing perk.

#### Dev Nair, age 16, football
**Core:** The best left foot Lucia has coached. A scout from a Sterling-sponsored academy wants him, but he'd have to leave his mum alone.

1. "We need a keeper. Ji-ho is terrible." `[hearts only]`
2. Blue slush vs red slush: a heated debate. `[hearts only]`
3. Thursday pasta night: he asks you for vegetables to make it "actually good". `[quest]`
4. You watch him at practice; he's clearly the best. `[route: watch practice]`
5. A scout watches him from the field side. `[+Lucia Part 5]`
6. The academy offer: boarding school in the city, all paid, sponsored by Sterling. `[hearts only]`
7. He doesn't tell Priya. You find out because Lucia asks you what to do. `[+Lucia Part 7]`
8. ⚑ **Fork:** encourage him to take the academy place, or to stay, play the Suburb Cup and look after his mum.
9. **Academy:** he comes home Sundays in a Sterling tracksuit and hates it a little. **Stay:** he leads the team in the Cup final.
10. He scores in the final (or, after the academy ending, plays *against* the suburb and passes the winning goal to Ji-ho by "accident").

**Other directions:** (a) a cooking arc (he wants to be a chef, not a footballer); (b) he and Ji-ho start a bike delivery business.

---

### HOUSE 4: The roommates

#### Bea Lindqvist, botany student
**Core:** Forty questions about your soil. Her thesis becomes the scientific proof of pollution, and her supervisor's grant comes from Sterling.

1. "You're basically my hero." The list of forty questions. `[hearts only]`
2. She asks to take a soil sample from your farm. `[hearts only]`
3. Quest: bring her one of each crop to study. `[quest]`
4. The seed-rack friendship with Soo-ah (and Ada on Tuesdays). `[+Soo-ah Part 5]` (optional romance)
5. She helps Nia with the science-fair project. `[+Nia Part 5]`
6. Her samples from your top rows show heavy metals. `[+RiverPollution.Level ≥ 2]`
7. Her supervisor tells her to leave those samples out of the thesis. `[hearts only]`
8. ⚑ **Fork:** publish the true results (lose the grant), or keep the grant and give *you* the data in secret.
9. **Publish:** her thesis is quoted at city hall. **Secret:** she finishes her degree and you get the evidence item.
10. She opens a small soil lab in the Kims' back room and tests the whole suburb's gardens. Your soil items (from `SoilCare`) get a small bonus.

**Other directions:** (a) a plain romance with Soo-ah; (b) Bea wants a field station on your farm (a building you can unlock).

#### Tariq, busker
**Core:** "The street's soundtrack." He wants to make a real album. Sterling wants to buy his best song for a tower advert.

1. "Requests are free. Taste is extra." `[hearts only]`
2. Gus "hates" his songs and knows all the words. `[hearts only]`
3. Quest: bring him something to inspire a song (a gift item: a feather, a flower, a fish). `[quest]`
4. He plays you a new song about the farm. `[hearts only]`
5. Bea labels everything, including him. A roommate comedy beat. `[+Bea Part 3]`
6. His city café gig is cancelled. The café is now a Sterling coffee chain. `[+City scene]`
7. A Sterling marketing person offers a lot of money for "Maple Lane" as a jingle. `[hearts only]`
8. ⚑ **Fork:** sell the song (and pay for the album), or keep it and record the album on cheap gear in Frank's store.
9. **Sell:** he hears his own song in the tower lobby and can't stand it. **Keep:** Gus secretly lends him money to record.
10. A concert on the plaza stage (if Walt and Gus built one) with the whole street. Gus sings the chorus.

**Other directions:** (a) he's a mornings-are-a-rumour slacker who learns to work at your farm; (b) a song for each neighbour, a "Street Album" collected over the arc.

---

### HOUSE 5: The Tanakas

#### Yuki Tanaka, museum curator
**Core:** Precise and quietly funny. Her new exhibition is about the valley before the city, and the museum's new sponsor, Sterling, wants it rewritten.

1. "If you dig up anything interesting, I'd love to see it first." `[hearts only]`
2. Frank's sign is crooked by two degrees. She hasn't said anything. It's very hard. `[hearts only]`
3. Quest: bring her an artifact (from tilling, fishing or the mine). `[quest]`
4. She and Grace compare notes on your survey marker. `[+Grace Part 4]`
5. She frets about her father: he's slowing down, and she's too busy to notice. `[+Kenji Part 4]`
6. The sponsor's note: "Remove the wetland section." `[hearts only]`
7. She invites you to the museum after hours to see the real exhibition. `[+City scene]`
8. ⚑ **Fork:** open the honest exhibition (and lose the sponsor), or move it to a free pop-up in the suburb.
9. **Honest:** city papers review it and Victor's mood drops. **Pop-up:** the Kims' back room becomes a tiny museum with your artifacts.
10. She takes a Monday off with her father, with no plans at all, and the sign at Frank's is finally straight.

**Other directions:** (a) a "collector" arc where you complete a museum collection like a checklist; (b) she has a past with Victor Sterling (they studied together).

#### Kenji Tanaka, retired, chess and tai chi
**Core:** Gentle wisdom. He wants to make friends with the crow before he moves back to Japan, a decision he hasn't told Yuki about.

1. "Sit, if you have time. Stand, if you do not." `[hearts only]`
2. He greets the crow every morning. The crow ignores him. `[hearts only]`
3. He teaches you a tai-chi move. (Optional: a small stamina perk.) `[hearts only]`
4. He forgets a chess opening and laughs it off. Yuki doesn't laugh. `[hearts only]`
5. Quest: bring the crow a treat it might like (an item test, many wrong answers). `[quest]`
6. He tells you he's thinking of going home to Japan, to his brother. `[hearts only]`
7. He asks you to help him write a letter to Yuki, because he can't say it aloud. `[quest, mail]`
8. ⚑ **Fork:** encourage him to go, or encourage him to tell Yuki and decide together.
9. **Go:** a farewell party in the plaza; Walt cries and denies it. **Tell her:** Yuki surprises him. She'll come with him for a season.
10. The crow lands on the chess board and says hello. (Or, after he goes, it says hello to you instead, and a postcard from Japan arrives.)

**Other directions:** (a) a heavier memory-loss arc, gently written; (b) Kenji was once an engineer at the old water works and knows how the river was poisoned (a main-plot piece).

---

### HOUSE 6: The Bells

#### Marcus Bell, works at the tower
**Core:** The insider. He hates his job and feels guilty, and is the only neighbour who can reach Sterling's paperwork.

1. "Don't tell Frank. Or the Kims." `[hearts only]`
2. He jogs past Kenji and the crow. He's always second. `[hearts only]`
3. He buys local on weekends "out of guilt". Quest: sell him a crop directly. `[quest]`
4. He's sent to the Kims with the franchise offer and hates every second. `[+Jae-hoon Part 7]`
5. He sees shipping manifests that list "river disposal". `[hearts only]`
6. Lena tells you he isn't sleeping. `[+Lena Part 5]`
7. He asks you to see for yourself, in the tower's back room. `[+flag saw_tower_dumping]`
8. ⚑ **Fork:** help him blow the whistle (bring copies to city hall), or let him take a promotion that "lets him change it from inside".
9. **Whistle:** he loses his job, and the street rallies around him. **Promotion:** he gets a window office and avoids your eyes.
10. **Whistle:** he works at the Frank's co-op or city hall, and jogs past Kenji *first*. **Promotion:** he finally quits, a season later, with a box of documents under his arm.

**Other directions:** (a) Lena is expecting a baby, which is why he can't quit (raises the stakes); (b) he's a double agent Victor planted to watch the suburb, and the twist is that he flips.

#### Lena Bell, illustrator
**Core:** She's drawing every house on the street for a picture book. A big publisher wants it, if she makes the street "less shabby".

1. "You have a very good hat face." `[hearts only]`
2. She sketches your farm without asking and gives you the drawing (a decoration item). `[hearts only]`
3. Nia poses for beetle facts. `[+Nia Part 2]`
4. Quest: tell her a story about each of three houses (talk to their owners). `[quest]`
5. She worries about Marcus. `[+Marcus Part 5]`
6. She asks Sam to write the words. Sam panics, then says yes. `[+Sam Part 6]`
7. The publisher, a Sterling Media company, wants the curtains brighter, the cracks painted over, and the tower in the background "for context". `[hearts only]`
8. ⚑ **Fork:** let her sign with the big publisher, or print the book at the Kims' with a print run of fifty.
9. **Publisher:** the book is everywhere, and it doesn't look like home. **Fifty:** every house gets one copy.
10. Book launch at the plaza. The last page is your farm at dawn.

**Other directions:** (a) a mural on the general store wall instead of a book; (b) a self-doubt arc, where she stops drawing and you help her start again.

---

### HOUSE 7: Sam

#### Sam Whitaker, writer
**Core:** Lonely, shy, stuck on a novel. Works three mornings a week at the treatment plant and sees exactly what the tower throws away.

1. "I'm not good at the talking part." `[hearts only]`
2. The soup appears again. Sam has never caught who leaves it. `[hearts only]`
3. Sam reads you one sentence. Then deletes it. `[hearts only]`
4. Sam mentions barrels at the plant marked with the tower's logo. `[+trash_treated ≥ 1 or a visit to the plant]`
5. You tell Sam who leaves the soup (if Rosa allows it). `[+Rosa Part 7]`
6. Lena asks Sam to write the street book. `[+Lena Part 6]`
7. Sam starts writing an exposé about the plant instead of the novel. `[hearts only]`
8. ⚑ **Fork:** publish the exposé (powerful, scary), or finish the autumn novel (personal, gentle) and hand the plant notes to Marcus.
9. **Exposé:** it runs in the city paper, and Sam is suddenly famous and terrified. **Novel:** Sam reads chapter one aloud in the plaza.
10. Sam brings soup to Rosa's step. The bowl comes back washed.

**Other directions:** (a) a friends-to-romance option with the player; (b) Sam is secretly a once-famous author hiding from a bestseller.

---

### HOUSE 8: The Reyes siblings

#### Mateo Reyes, park gardener
**Core:** "Eleven kinds of grass and a great deal of worry." City hall plans to sell part of the park to Sterling for a car park, and his sister is the one stamping the forms.

1. "You can't hurry a plant." `[hearts only]`
2. He and Ada check each other's planters. `[hearts only]`
3. Quest: he asks for a specific tree seedling or flower for the park. `[quest]`
4. The blossom week; everyone thanks him. `[+season Spring]`
5. Surveyors with Sterling vests appear on the football field. `[hearts only]`
6. Lucia admits the sale paperwork is on her desk. `[+Lucia Part 6]`
7. The river-side trees are dying. `[+RiverPollution.Level ≥ 3]`
8. ⚑ **Fork:** stage a "Save the Park" day (everyone plants a tree), or accept a deal: Sterling keeps half and pays to clean the river.
9. **Save:** the whole street plants trees, and the surveyors leave. **Deal:** a car park on half the field, but the river trees recover.
10. A bench in the park with a plaque for Mateo, which he hates and secretly loves.

**Other directions:** (a) a quiet arc about his ambition to be a botanic garden head in the city; (b) friendship with Bea, who teaches him the Latin names.

#### Lucia Reyes, city hall and football coach
**Core:** Energy for ten people. She wants to *do good* at city hall, but the system makes her stamp Sterling's forms. Coaching is where she's happiest.

1. "Ever played left back?" `[hearts only]`
2. "Bring a sandwich and a book" to city hall. `[hearts only]`
3. She signs you up for the Suburb Cup squad. You can't refuse. `[hearts only]`
4. City hall jobs ("cityq_" counters) are hers to hand out. She gives you the best ones. `[+any cityq_ counter]`
5. The scout watching Dev. `[+Dev Part 5]`
6. The park sale paperwork. She hasn't signed it. `[+Mateo Part 5]`
7. Nia vs science fair; Dev vs academy. She's worried for both of them. `[+Nia Part 7 or Dev Part 7]`
8. ⚑ **Fork:** she runs for city council against Sterling's candidate, or stays at her desk and "loses" the park paperwork.
9. **Council:** campaign days in the plaza (you can help). **Desk:** the paperwork gets lost very carefully.
10. The Suburb Cup final on Mateo's pitch. She wins or loses the election that same week.

**Other directions:** (a) a pure sports arc (the Cup is the whole thing); (b) she was once a pro footballer and her injury is a secret.

---

### HOUSE 9: Ada

#### Ada Kowalczyk, retired florist
**Core:** She ran a city flower stall for thirty years until the tower's flower department closed it. She says she's not bitter. She is, a bit. And nobody has ever seen her cat.

1. "Never tell a rose it looks tired." `[hearts only]`
2. Someone's been sitting on the red tulips. `[hearts only]`
3. Quest: grow a specific flower (Gold quality). `[quest, crop quality]`
4. She tells you about the stall and the day the tower opened. `[hearts only]`
5. She mentors Bea and Soo-ah at the seed rack. `[+Bea Part 4]`
6. Bloom Show announced in the plaza. She plans to enter "for fun". `[+season Spring]`
7. The tower enters the Bloom Show with plastic-perfect flowers. `[hearts only]`
8. ⚑ **Fork:** she reopens a flower stall in the plaza, or she gives her secret recipes and stall name to Bea.
9. **Stall:** "Kowalczyk & Farm" with your flowers. **Bea:** Ada watches Bea win the show and cries in the planters.
10. You finally meet Pan Tadeusz the cat. He is enormous.

**Other directions:** (a) a lost-love arc (a letter from Emeka's dead-letter box was for her); (b) a rivalry with Mateo over the plaza planters that becomes a friendship.

---

### HOUSE 10: Gus

#### Gus Petrakis, retired builder
**Core:** He heckles everyone because he's lonely. He's a widower, Tariq's songs remind him of his late wife's music, and the job he took from Sterling thirty years ago broke his partnership with Walt.

1. "Ask me how I WOULD have built it. That's free." `[hearts only]`
2. He critiques your house, kindly. `[hearts only]`
3. He mutters along to Tariq's chorus. `[+Tariq Part 2]`
4. Rain day: he checks "whose roof is his" and finds a leak on Frank's store. `[+weather Rain]`
5. He finds out Frank sold his house. Their argument. `[+Frank Part 5]`
6. He tells you about Eleni, his late wife, and the songs she loved. `[hearts only]`
7. He tells you the Sterling-tower job story from his side: he needed the money for Eleni's hospital bills. `[+Walt Part 6]`
8. ⚑ **Fork:** help him apologise to Walt, or help him confess to Frank and put his savings into the co-op.
9. **Walt:** they build the plaza stage together. **Frank:** Gus's savings save the store, and he never mentions it.
10. He sings Eleni's song with Tariq on the plaza stage. Walt and Frank are in the front row.

**Other directions:** (a) health: Gus has a fall, and the street takes turns looking after him; (b) Gus teaches *you* to build, unlocking a building skill or perk.

---

## 5. Putting it together: the arc map

How the forks feed the main plot (if you build option C):

| Choice | Pro-community result | Pro-Sterling result |
|---|---|---|
| Jae-hoon: local supplier vs franchise | You get a weekly sales outlet | The store sells Sterling goods |
| Frank: co-op vs sale | Workshops, Frank's new home | Frank's becomes a Sterling store |
| Emeka: petition vs job | Postcards and news continue | Mail stops; you hear less news |
| Grace / Bea / Priya / Sam / Marcus | Evidence goes to city hall | Evidence handed to you in secret instead |
| Mateo / Lucia: save the park vs deal | Full park, trees planted | Half a car park, cleaner river |
| Rosa / Ada: market vs tower | A Saturday plaza market | Plastic-wrapped bread and flowers |

A good rule: **the "Sterling" side should never be a fail state.** It's a sadder version of the town, and most of those arcs leave a way back.

**The Tower File** (main-plot evidence) has six pieces: Marcus's manifests, Sam's barrels, Grace's maps, Bea's soil data, Priya's records, and Lucia's permits. Collecting 4 of 6 could unlock a final confrontation with Victor (flag `ceo_confronted` already exists).

---

## 6. What would need new code

- **Now built (story arc system):** parts gated by hearts + conditions, one part per day, item quests in the journal (with minimum quality), the Part 8 choice with A/B paths, flags set by parts and choices, rewards, progress in the People tab.
- **Already worked before:** hearts-gated and flag-gated chat lines (`NpcChatLine.conditions`: counter `friend_<id>` ≥ N×100), schedule routes gated by flags, gifts.
- **Small additions:**
  - A way for a chat line to **set a flag** when it's read (so Part 5 can depend on "you heard Part 4"). Right now `ScheduledNpc` has no such hook.
  - A **"story quest"** component that adds and completes `QuestLog` quests from data instead of a script per quest.
  - **Story mail**: Emeka posts a letter when a flag is set.
- **Bigger additions:**
  - **Dialogue choices** for the ⚑ forks. `ScheduledNpc` currently only offers built-in choices (gifts, shop, Frank's hardware), not story choices.
  - **Scene moments** (two NPCs talking while you watch; the plaza concert, the Cup final).
  - **Events in the plaza** (market, Bloom Show, the Cup): temporary props plus everyone's schedule redirected for one day.
- **New characters** (optional): Tomás Delgado, the academy scout, the Sterling marketing person, the city historian.

---

## 7. Suggested first step

Pick **three arcs** to build fully first, so you test the system before writing 230 parts into the game:

1. **Rosa + Sam (the Soup Mystery)**: small, sweet, and uses only chat lines and one route.
2. **Marcus**: proves the main-plot gates (`saw_tower_dumping`) work.
3. **Frank**: shows that the flagship shopkeeper has a heart, and sets up the co-op.
