namespace PosTpv.Web.Components.Layout;

/// <summary>
/// Per-circuit toggle state for the navbar's "more" group. NavMenu (the toggle button and the
/// admin-ish links/title it reveals) and TopbarExtras (theme switch, user name, sign-out) are
/// siblings under the static MainLayout, so they share this scoped service instead of a
/// cascading parameter to stay in sync.
/// </summary>
public class NavMoreState
{
    public bool IsOpen { get; private set; }

    public event Action? Changed;

    public void Toggle()
    {
        IsOpen = !IsOpen;
        Changed?.Invoke();
    }
}
