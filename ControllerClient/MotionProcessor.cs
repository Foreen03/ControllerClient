using System;

namespace ControllerClient
{
    internal sealed class MotionProcessor
    {
        private readonly MotionSettings settings;

        private float smoothedTurn;
        private float moveVelocity;
        private int lastStepCount;

        public MotionProcessor(MotionSettings settings)
        {
            this.settings = settings;
        }

        public MotionIntent Update(
            float ax,
            float ay,
            float az,
            int stepCount,
            float stepsCadence,
            float deltaTime,
            long timestamp
        )
        {
            float magnitude = MathF.Sqrt(ax * ax + ay * ay + az * az);
            if (magnitude < 0.0001f) return default;

            float rawSteering = ax / magnitude;
            float targetSteering = 0f;

            float absSteering = Math.Abs(rawSteering);
            if (absSteering > settings.DeadZone)
            {
                targetSteering = (absSteering - settings.DeadZone) / (1.0f - settings.DeadZone);
                targetSteering *= Math.Sign(rawSteering);
            }

            targetSteering = Math.Clamp(targetSteering / settings.MaxTilt, -1f, 1f);

            smoothedTurn = Lerp(smoothedTurn, targetSteering, settings.SteeringSmoothing);

            // ---------- Steps → Forward ----------
            int stepDelta = stepCount - lastStepCount;
            lastStepCount = stepCount;

            if (stepDelta > 0)
                moveVelocity += stepDelta * settings.StepImpulse;

            moveVelocity = Math.Min(moveVelocity, settings.MaxMove);
            moveVelocity = Damp(moveVelocity, settings.MoveDamping, deltaTime);

            return new MotionIntent
            {
                Move = moveVelocity,
                Turn = smoothedTurn,
                Timestamp = timestamp,
                rawSteps = stepCount,
                StepsCadence = stepsCadence
            };
        }

        private static float Damp(float value, float damping, float dt)
        {
            return value * MathF.Exp(-damping * dt);
        }

        private static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * t;
        }
    }
}