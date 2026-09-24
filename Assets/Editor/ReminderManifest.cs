#if UNITY_ANDROID
using System.IO;
using System.Xml;
using UnityEditor.Android;
using UnityEngine;

namespace BlockPuzzle.EditorTools
{
    /// <summary>
    /// Wires the daily reminder into every Android build: the two permissions it needs, its two
    /// receivers (Assets/Plugins/Android/PrizmaReminder*.java), and the notification's small icon.
    ///
    /// Written into the generated Gradle project rather than kept in a custom manifest, so the
    /// Player Settings stay as they are and nothing in Assets has to be kept in step by hand.
    /// The icon is a vector drawable — a few lines of path data, drawn in code like every other
    /// graphic in the game: the prism, flat white, which is what Android wants in the status bar.
    /// </summary>
    sealed class ReminderManifest : IPostGenerateGradleAndroidProject
    {
        const string AndroidNs = "http://schemas.android.com/apk/res/android";
        const string Package = "com.emirhankayabas.prizma.reminder";

        public int callbackOrder => 10;

        public void OnPostGenerateGradleAndroidProject(string unityLibraryPath)
        {
            string manifestPath = Path.Combine(unityLibraryPath, "src", "main", "AndroidManifest.xml");
            if (!File.Exists(manifestPath))
            {
                Debug.LogError("PRIZMA: AndroidManifest.xml bulunamadı, hatırlatıcı eklenmedi: " + manifestPath);
                return;
            }

            var doc = new XmlDocument();
            doc.Load(manifestPath);
            var manifest = doc.DocumentElement;
            var application = manifest?.SelectSingleNode("application") as XmlElement;
            if (manifest == null || application == null)
            {
                Debug.LogError("PRIZMA: manifest'te <application> yok, hatırlatıcı eklenmedi.");
                return;
            }

            // Android 13+: the notification itself needs the player's permission, asked in game.
            AddPermission(doc, manifest, "android.permission.POST_NOTIFICATIONS");
            // Alarms are cleared on reboot; this lets the reminder put itself back.
            AddPermission(doc, manifest, "android.permission.RECEIVE_BOOT_COMPLETED");

            AddReceiver(doc, application, Package + ".PrizmaReminderReceiver", exported: false);
            var boot = AddReceiver(doc, application, Package + ".PrizmaReminderBoot", exported: true);
            if (boot != null && boot.SelectSingleNode("intent-filter") == null)
            {
                var filter = doc.CreateElement("intent-filter");
                foreach (var action in new[] { "android.intent.action.BOOT_COMPLETED", "android.intent.action.MY_PACKAGE_REPLACED" })
                {
                    var node = doc.CreateElement("action");
                    node.SetAttribute("name", AndroidNs, action);
                    filter.AppendChild(node);
                }
                boot.AppendChild(filter);
            }

            doc.Save(manifestPath);

            string drawable = Path.Combine(unityLibraryPath, "src", "main", "res", "drawable");
            Directory.CreateDirectory(drawable);
            File.WriteAllText(Path.Combine(drawable, "prizma_notify.xml"), NotifyIcon);
        }

        static void AddPermission(XmlDocument doc, XmlElement manifest, string name)
        {
            foreach (XmlElement existing in manifest.SelectNodes("uses-permission"))
                if (existing.GetAttribute("name", AndroidNs) == name) return;

            var node = doc.CreateElement("uses-permission");
            node.SetAttribute("name", AndroidNs, name);
            manifest.PrependChild(node);
        }

        static XmlElement AddReceiver(XmlDocument doc, XmlElement application, string name, bool exported)
        {
            foreach (XmlElement existing in application.SelectNodes("receiver"))
                if (existing.GetAttribute("name", AndroidNs) == name) return existing;

            var node = doc.CreateElement("receiver");
            node.SetAttribute("name", AndroidNs, name);
            node.SetAttribute("exported", AndroidNs, exported ? "true" : "false");
            application.AppendChild(node);
            return node;
        }

        /// <summary>
        /// The status-bar glyph: a prism — a triangle with a light beam entering on the left and
        /// fanning out on the right — in a 24dp box. Android tints it; only the shape counts.
        /// </summary>
        const string NotifyIcon =
@"<vector xmlns:android=""http://schemas.android.com/apk/res/android""
    android:width=""24dp"" android:height=""24dp""
    android:viewportWidth=""24"" android:viewportHeight=""24"">
    <path android:fillColor=""#FFFFFFFF""
        android:pathData=""M12,3.2L20.6,18.4L3.4,18.4Z M12,7.4L7.1,16.1L16.9,16.1Z""
        android:fillType=""evenOdd""/>
    <path android:fillColor=""#FFFFFFFF""
        android:pathData=""M1,12.6L9.2,11.2L9.5,12.4L1.3,13.8Z""/>
    <path android:fillColor=""#FFFFFFFF""
        android:pathData=""M14.6,11.6L23,9.2L23,10.6L14.9,12.8Z M14.9,13.6L23,13.4L23,14.8L15.1,14.8Z""/>
</vector>";
    }
}
#endif
