namespace ControllerClient
{
    /// <summary>
    /// Result of a GPX export request.
    /// Delivered asynchronously via the <see cref="Controller.OnGpxExported"/> event.
    /// </summary>
    public sealed class GpxExportResult
    {
        /// <summary>Full path to the saved GPX file on the PC server's disk.</summary>
        public string FilePath { get; set; }

        /// <summary>Total distance walked during the session, in kilometres.</summary>
        public double DistanceKm { get; set; }

        /// <summary>Duration of the session (e.g. "00:25:43").</summary>
        public string Duration { get; set; }

        /// <summary>
        /// Non-null if the export failed on the server side.
        /// When set, FilePath/DistanceKm/Duration are not valid.
        /// </summary>
        public string Error { get; set; }
    }
}
