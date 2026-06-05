namespace ControllerClient
{
    public struct MotionIntent
    {
        public float Move;   // 0..1 (forward intent)
        public float Turn;   // -1..1 (steering intent)
        public long Timestamp;
        public int rawSteps;
        public float StepsCadence;
    }
}
