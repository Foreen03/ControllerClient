namespace ControllerClient
{
    /// <summary>
    /// Result of a screenshot capture request.
    /// The screenshot is saved as a JPEG file on the PC server's disk.
    /// Read the file at FilePath to access the image data.
    /// </summary>
    public sealed class ScreenshotResult
    {
        /// <summary>Full path to the saved JPEG file on disk.</summary>
        public string FilePath { get; set; }

        /// <summary>Width of the captured image in pixels.</summary>
        public int Width { get; set; }

        /// <summary>Height of the captured image in pixels.</summary>
        public int Height { get; set; }
    }
}
