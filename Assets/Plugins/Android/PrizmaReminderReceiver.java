package com.emirhankayabas.prizma.reminder;

import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;

/** The reminder's alarm. Not exported: only our own PendingIntent reaches it. */
public final class PrizmaReminderReceiver extends BroadcastReceiver {
    @Override
    public void onReceive(Context context, Intent intent) {
        PrizmaReminder.fire(context);
    }
}
