namespace ControllerClient
{
    public sealed class MotionSettings
    {
        // Steering
        public float MaxTilt = 0.6f;
        public float DeadZone = 0.05f;
        public float SteeringSmoothing = 0.12f;
        public float TurnSpeedDeg = 120f;

        // Movement
        public float StepImpulse = 0.4f;
        public float MaxMove = 1.5f;
        public float MoveDamping = 2.0f;
    }
}
