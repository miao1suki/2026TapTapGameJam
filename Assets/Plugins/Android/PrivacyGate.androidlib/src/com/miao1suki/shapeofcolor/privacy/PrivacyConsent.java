package com.miao1suki.shapeofcolor.privacy;

import android.app.Activity;
import android.content.Context;
import android.content.Intent;
import android.util.AtomicFile;
import java.io.ByteArrayOutputStream;
import java.io.File;
import java.io.FileOutputStream;
import java.io.InputStream;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;

/** No Unity, analytics, WebView, network, device identifiers or permission requests. */
public final class PrivacyConsent {
    public static final String POLICY_URL = "https://www.taptap.cn/app/976206/common-privacy";
    private static String version;
    private PrivacyConsent() {}

    static byte[] read(InputStream input) throws Exception {
        try (InputStream stream = input; ByteArrayOutputStream bytes = new ByteArrayOutputStream()) {
            byte[] buffer = new byte[4096]; int length;
            while ((length = stream.read(buffer)) != -1) bytes.write(buffer, 0, length);
            return bytes.toByteArray();
        }
    }

    static String policy(Context context) throws Exception {
        return new String(read(context.getAssets().open("privacy-policy.html")), StandardCharsets.UTF_8);
    }

    static synchronized String version(Context context) throws Exception {
        if (version == null) {
            byte[] digest = MessageDigest.getInstance("SHA-256").digest(read(context.getAssets().open("privacy-policy.html")));
            StringBuilder hex = new StringBuilder();
            for (byte value : digest) hex.append(String.format(java.util.Locale.ROOT, "%02x", value & 255));
            version = hex.toString();
        }
        return version;
    }

    private static AtomicFile record(Context context) {
        // Not backed up or restored onto another installation. Both processes read the same atomic file.
        return new AtomicFile(new File(context.getNoBackupFilesDir(), "privacy-consent-version"));
    }

    private static AtomicFile gameProcess(Context context) {
        return new AtomicFile(new File(context.getNoBackupFilesDir(), "privacy-game-process"));
    }

    static void registerGameProcess(Context context) {
        AtomicFile file = gameProcess(context); FileOutputStream stream = null;
        try {
            stream = file.startWrite();
            stream.write(Integer.toString(android.os.Process.myPid()).getBytes(StandardCharsets.UTF_8));
            file.finishWrite(stream);
        } catch (Exception error) {
            if (stream != null) file.failWrite(stream);
            throw new IllegalStateException("Cannot register consent process", error);
        }
    }

    public static boolean hasConsent(Context context) {
        try { return version(context).equals(new String(record(context).readFully(), StandardCharsets.UTF_8)); }
        catch (Exception ignored) { return false; }
    }

    static boolean accept(Context context) {
        AtomicFile file = record(context); FileOutputStream stream = null;
        try {
            stream = file.startWrite(); stream.write(version(context).getBytes(StandardCharsets.UTF_8));
            file.finishWrite(stream); return true;
        } catch (Exception ignored) { if (stream != null) file.failWrite(stream); return false; }
    }

    static Intent gate(Context context, boolean review) {
        return new Intent().setClassName(context.getPackageName(), PrivacyActivity.class.getName())
            .putExtra("review", review).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK | Intent.FLAG_ACTIVITY_CLEAR_TOP);
    }

    public static void showPolicy(Activity activity) { activity.startActivity(gate(activity, true)); }

    public static void withdrawConsentAndExit(Activity activity) {
        record(activity).delete();
        int current = android.os.Process.myPid();
        int game = -1;
        try {
            game = Integer.parseInt(new String(gameProcess(activity).readFully(), StandardCharsets.UTF_8));
            if (game > 0 && game != current) android.os.Process.killProcess(game);
        } catch (Exception ignored) { /* A stopped process has nothing left to revoke. */ }
        gameProcess(activity).delete();
        activity.startActivity(gate(activity, false));
        if (game == current) { android.os.Process.killProcess(current); System.exit(0); }
        else activity.finish();
    }
}
