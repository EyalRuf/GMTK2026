namespace NineLives
{
    /// A one-time pickup that changes the nature of the player's next corpse,
    /// and sometimes grants an extra ability for the rest of the current life.
    /// Consumed (reset to None) the moment that life ends.
    public enum UpgradeType
    {
        None,
        Trampoline,
        Carry,
    }

    public enum CorpseKind
    {
        Normal,
        Trampoline,
    }
}
