package com.emirhankayabas.prizma.reminder;

import android.app.AlarmManager;
import android.app.Notification;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;

/**
 * The daily puzzle's reminder: one notification at the time of day the player last solved it.
 *
 * Called from C# (Reminder.cs). No plugin and no network: an AlarmManager alarm wakes
 * PrizmaReminderReceiver, which posts the notification and arms the next day. The texts are
 * written by the game at scheduling time, in the player's language, and kept here so the
 * receiver never has to start Unity to find them.
 *
 * Inexact on purpose (setAndAllowWhileIdle): an exact alarm needs its own permission, and a
 * reminder that lands a few minutes late loses nothing.
 */
public final class PrizmaReminder {
    static final String PREFS = "prizma_reminder";
    static final String CHANNEL = "prizma_daily";
    static final int ID = 4711;
    static final long DAY = 24L * 60L * 60L * 1000L;

    /** Unanswered reminders before it goes quiet. A game that keeps calling is uninstalled. */
    static final int MAX_UNANSWERED = 3;

    private PrizmaReminder() { }

    /** Arms the reminder for an absolute time (ms since the epoch). Replaces any earlier one. */
    public static void schedule(Context context, long atMillis, String title, String body, String followUp, String channelName) {
        prefs(context).edit()
            .putLong("at", atMillis)
            .putString("title", title)
            .putString("body", body)
            .putString("followUp", followUp)
            .putString("channel", channelName)
            .putInt("unanswered", 0)
            .apply();

        ensureChannel(context, channelName);
        arm(context, atMillis);
    }

    public static void cancel(Context context) {
        prefs(context).edit().remove("at").apply();
        AlarmManager alarms = (AlarmManager) context.getSystemService(Context.ALARM_SERVICE);
        if (alarms != null) alarms.cancel(pending(context));
    }

    /** False when the player has switched the app's notifications off in the system settings. */
    public static boolean enabled(Context context) {
        NotificationManager manager = (NotificationManager) context.getSystemService(Context.NOTIFICATION_SERVICE);
        return manager != null && manager.areNotificationsEnabled();
    }

    // ------------------------------------------------------------------ receiver side

    /** The alarm went off: post the reminder and arm the same time tomorrow. */
    static void fire(Context context) {
        SharedPreferences p = prefs(context);
        long at = p.getLong("at", 0L);
        if (at == 0L) return;

        int unanswered = p.getInt("unanswered", 0);
        String text = unanswered == 0 ? p.getString("body", "") : p.getString("followUp", "");
        post(context, p.getString("title", ""), text, p.getString("channel", "PRIZMA"));

        unanswered++;
        if (unanswered >= MAX_UNANSWERED) {
            p.edit().remove("at").apply();
            return;
        }

        long next = at + DAY;
        long now = System.currentTimeMillis();
        while (next <= now) next += DAY;
        p.edit().putLong("at", next).putInt("unanswered", unanswered).apply();
        arm(context, next);
    }

    /** Alarms do not survive a reboot or an update; this puts the pending one back. */
    static void rearm(Context context) {
        SharedPreferences p = prefs(context);
        long at = p.getLong("at", 0L);
        if (at == 0L) return;

        long now = System.currentTimeMillis();
        while (at <= now) at += DAY;
        p.edit().putLong("at", at).apply();
        arm(context, at);
    }

    // ------------------------------------------------------------------ plumbing

    static SharedPreferences prefs(Context context) {
        return context.getSharedPreferences(PREFS, Context.MODE_PRIVATE);
    }

    static void arm(Context context, long atMillis) {
        AlarmManager alarms = (AlarmManager) context.getSystemService(Context.ALARM_SERVICE);
        if (alarms == null) return;
        alarms.setAndAllowWhileIdle(AlarmManager.RTC_WAKEUP, atMillis, pending(context));
    }

    static PendingIntent pending(Context context) {
        Intent intent = new Intent(context, PrizmaReminderReceiver.class);
        return PendingIntent.getBroadcast(context, ID, intent,
            PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE);
    }

    static void ensureChannel(Context context, String name) {
        NotificationManager manager = (NotificationManager) context.getSystemService(Context.NOTIFICATION_SERVICE);
        if (manager == null) return;
        NotificationChannel channel = new NotificationChannel(CHANNEL, name, NotificationManager.IMPORTANCE_DEFAULT);
        channel.setShowBadge(true);
        manager.createNotificationChannel(channel);
    }

    static void post(Context context, String title, String text, String channelName) {
        NotificationManager manager = (NotificationManager) context.getSystemService(Context.NOTIFICATION_SERVICE);
        if (manager == null || !manager.areNotificationsEnabled()) return;
        ensureChannel(context, channelName);

        Notification.Builder builder = new Notification.Builder(context, CHANNEL)
            .setSmallIcon(icon(context))
            .setContentTitle(title)
            .setContentText(text)
            .setStyle(new Notification.BigTextStyle().bigText(text))
            .setCategory(Notification.CATEGORY_REMINDER)
            .setAutoCancel(true);

        PendingIntent open = launch(context);
        if (open != null) builder.setContentIntent(open);

        manager.notify(ID, builder.build());
    }

    /** The prism glyph the build writes into the project; the app icon if it is missing. */
    static int icon(Context context) {
        int id = context.getResources().getIdentifier("prizma_notify", "drawable", context.getPackageName());
        return id != 0 ? id : context.getApplicationInfo().icon;
    }

    static PendingIntent launch(Context context) {
        Intent intent = context.getPackageManager().getLaunchIntentForPackage(context.getPackageName());
        if (intent == null) return null;
        intent.setFlags(Intent.FLAG_ACTIVITY_NEW_TASK | Intent.FLAG_ACTIVITY_RESET_TASK_IF_NEEDED);
        return PendingIntent.getActivity(context, ID, intent,
            PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE);
    }
}
