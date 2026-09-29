namespace PersonalArena.View
{
    /// <summary>
    /// Holds edge-triggered skill presses until a simulation tick consumes them.
    /// Block is level-triggered and has priority over all queued presses.
    /// </summary>
    public sealed class InputLatch
    {
        private bool strikePressed;
        private bool kickPressed;
        private bool dashPressed;
        private bool blockHeld;

        public void Capture(bool strikeWasPressed, bool kickWasPressed, bool dashWasPressed, bool isBlockHeld)
        {
            strikePressed |= strikeWasPressed;
            kickPressed |= kickWasPressed;
            dashPressed |= dashWasPressed;
            blockHeld = isBlockHeld;
        }

        public int ConsumeSkill()
        {
            int skill = blockHeld ? 3 : strikePressed ? 1 : kickPressed ? 2 : dashPressed ? 4 : 0;
            strikePressed = false;
            kickPressed = false;
            dashPressed = false;
            return skill;
        }

        public void Clear()
        {
            strikePressed = false;
            kickPressed = false;
            dashPressed = false;
            blockHeld = false;
        }
    }
}
