namespace NineLives
{
    /// Implemented by any level-content component that holds runtime state which must be
    /// restored when the level is (re)entered. Since level objects now persist in the scene
    /// and are enabled/disabled instead of destroyed, GameManager walks the active level's
    /// children and calls ResetToInitial() to wipe the previous attempt's state.
    public interface ILevelResettable
    {
        void ResetToInitial();
    }
}
