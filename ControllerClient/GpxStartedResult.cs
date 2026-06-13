namespace ControllerClient
{
    /// <summary>
    /// Result of a GPX start confirmation.
    /// Delivered asynchronously via the <see cref="Controller.OnGpxStarted"/> event.
    /// </summary>
    public sealed class GpxStartedResult
    {
        /// <summary>The mode the GPX session started in ("manual" or "simulated").</summary>
        public string Mode { get; set; }

        /// <summary>
        /// Non-null if the GPX session failed to initialize on the server side.
        /// </summary>
        public string Error { get; set; }
    }
}
