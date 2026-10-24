using ObjCRuntime;
using UIKit;

namespace SPOTTER.Mobile;

public class Program
{
    // This is the main entry point of the iOS application.
    static void Main(string[] args)
    {
        // UIApplicationMain creates the UIApplication, hands control to
        // AppDelegate, and never returns.
        UIApplication.Main(args, null, typeof(AppDelegate));
    }
}
