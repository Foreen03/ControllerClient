using System.Collections.Generic;
using System.Linq;

namespace ControllerClient
{
    public sealed class ActionState
    {
        private readonly Dictionary<string, bool> states = new Dictionary<string, bool>();
        private readonly Dictionary<string, long> lastUpdate = new Dictionary<string, long>();

        internal void Update(Dictionary<string, bool> snapshot, long timestamp)
        {
            foreach (var kv in snapshot)
            {
                states[kv.Key] = kv.Value;
                lastUpdate[kv.Key] = timestamp;
            }
        }

        public bool Get(string action)
        {
            return states.TryGetValue(action, out bool v) && v;
        }

        internal void ReleaseStaleButtons(long currentTimestamp, long timeoutMs = 150)
        {
            foreach (var key in states.Keys.ToList())
            {
                if (lastUpdate.TryGetValue(key, out var ts) &&
                    currentTimestamp - ts > timeoutMs)
                {
                    states[key] = false;
                }
            }
        }
    }

}
