namespace HomuraHimeAP.Core;

/// <summary>Turns "is held" into "has just been pressed": true for the one call in which the state goes from not held to held.</summary>
internal sealed class RisingEdge
{
    private bool _wasHeld;

    internal bool Update(bool held)
    {
        var rising = held && !_wasHeld;
        _wasHeld = held;
        return rising;
    }
}
