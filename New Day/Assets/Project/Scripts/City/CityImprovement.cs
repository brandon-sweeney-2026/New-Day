using UnityEngine;

/// <summary>
/// Shows its child objects only once a city improvement has been bought at City Hall.
/// Put it on an empty parent, type the improvement's id (the same id as on the City Hall Desk) and put the trees, flower beds...
/// under it as children. They are hidden until the flag city_imp_<id> is set, then they appear straight away.
/// </summary>
public class CityImprovement : MonoBehaviour
{
    [Tooltip("The id of the improvement on the City Hall Desk, for example trees.")]
    public string improvementId = "trees";
    [Tooltip("Tick to do the opposite: the children are shown UNTIL the improvement is bought (litter that gets cleaned up).")]
    public bool hideWhenBought = false;

    private void OnEnable()
    {
        GameState.OnStateChanged += OnStateChanged;
        Refresh();
    }

    private void OnDisable() { GameState.OnStateChanged -= OnStateChanged; }

    private void OnStateChanged(string key)
    {
        if (key == CityHallDesk.ImprovementFlag(improvementId)) Refresh();
    }

    private void Refresh()
    {
        bool bought = GameState.HasFlag(CityHallDesk.ImprovementFlag(improvementId));
        bool show = hideWhenBought ? !bought : bought;
        for (int i = 0; i < transform.childCount; i++)
        {
            GameObject child = transform.GetChild(i).gameObject;
            if (child.activeSelf != show) child.SetActive(show);
        }
    }
}
