package com.emirhankayabas.prizma.reminder;

import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;

/**
 * Puts the pending reminder back after a reboot or an app update, which both clear alarms.
 * Exported for the system's broadcasts; it only re-arms what is already saved, so a stray
 * broadcast from anywhere else can do nothing but that.
 */
public final class PrizmaReminderBoot extends BroadcastReceiver {
    @Override
    public void onReceive(Context context, Intent intent) {
        String action = intent == null ? null : intent.getAction();
        if (Intent.ACTION_BOOT_COMPLETED.equals(action) || Intent.ACTION_MY_PACKAGE_REPLACED.equals(action)) {
            PrizmaReminder.rearm(context);
        }
    }
}
