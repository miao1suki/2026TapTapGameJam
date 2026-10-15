package com.miao1suki.shapeofcolor.privacy;

import android.app.Application;
import android.content.Context;
import android.os.Build;
import java.io.FileInputStream;
import java.nio.charset.StandardCharsets;

/** Runs before ContentProvider/Activity creation, including Android 7.1 recent-task restoration. */
public final class PrivacyApplication extends Application {
    @Override protected void attachBaseContext(Context context) {
        super.attachBaseContext(context);
        String name = processName();
        if ((getPackageName() + ":privacy").equals(name)) return;
        if (!getPackageName().equals(name)) throw new IllegalStateException("Unapproved application process");
        if (!PrivacyConsent.hasConsent(this)) {
            // PrivacyActivity has its own process; terminate this process before Unity or providers can initialize.
            try { startActivity(PrivacyConsent.gate(this, false)); }
            finally { android.os.Process.killProcess(android.os.Process.myPid()); System.exit(0); }
        }
        PrivacyConsent.registerGameProcess(this);
    }

    private static String processName() {
        if (Build.VERSION.SDK_INT >= 28) return Application.getProcessName();
        // Own-process name only; do not query ActivityManager's list of other applications.
        try {
            byte[] bytes = PrivacyConsent.read(new FileInputStream("/proc/self/cmdline"));
            int length = 0; while (length < bytes.length && bytes[length] != 0) length++;
            return new String(bytes, 0, length, StandardCharsets.UTF_8);
        } catch (Exception error) { throw new IllegalStateException("Cannot verify privacy process", error); }
    }
}
