// Only ASCII characters can be used here.

namespace EpgTimer
{
    public partial class App : System.Windows.Application
    {
        internal const string VERSION_TEXT = (VERSION_TAG != "" ? " " + VERSION_TAG : "") + (VERSION_EXTRA != "" ? " " + VERSION_EXTRA : "");
        // Update date and time (yyyyMMddHHmm). Rewrite this on every update.
        internal const string VERSION_NUMBER = "202611241655";
        const string VERSION_TAG = "modified-" + VERSION_NUMBER;
        const string VERSION_EXTRA = "";
    }
}
