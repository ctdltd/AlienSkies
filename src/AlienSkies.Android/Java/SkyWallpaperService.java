package com.certificationtoolanddie.alienskies.shim;

import android.service.wallpaper.WallpaperService;
import android.view.SurfaceHolder;

/**
 * Thin Java shim for the live wallpaper.
 *
 * WallpaperService.Engine is a Java inner class, and the .NET for Android 10 wrapper generator
 * emits an invalid constructor ("super(p0)") for C# subclasses of inner classes. So the Engine
 * subclass lives here, in Java, and forwards every callback to a plain listener interface that
 * the C# side implements (see AlienSkiesWallpaper.cs).
 */
public abstract class SkyWallpaperService extends WallpaperService {

    /** Engine callbacks, implemented in C#. One listener per engine (real wallpaper and preview each get one). */
    public interface EngineListener {
        void onCreate(SurfaceHolder holder, boolean isPreview);
        void onVisibilityChanged(boolean visible);
        void onOffsetsChanged(float xOffset);
        void onSurfaceChanged(SurfaceHolder holder, int width, int height);
        void onSurfaceDestroyed(SurfaceHolder holder);
        void onDestroy();
    }

    /** Called once per engine; return a fresh listener. */
    protected abstract EngineListener createEngineListener();

    @Override
    public Engine onCreateEngine() {
        return new ShimEngine();
    }

    private final class ShimEngine extends Engine {
        private EngineListener listener;

        @Override
        public void onCreate(SurfaceHolder holder) {
            super.onCreate(holder);
            listener = createEngineListener();
            listener.onCreate(holder, isPreview());
        }

        @Override
        public void onVisibilityChanged(boolean visible) {
            if (listener != null) listener.onVisibilityChanged(visible);
        }

        @Override
        public void onOffsetsChanged(float xOffset, float yOffset, float xOffsetStep, float yOffsetStep,
                                     int xPixelOffset, int yPixelOffset) {
            if (listener != null) listener.onOffsetsChanged(xOffset);
        }

        @Override
        public void onSurfaceChanged(SurfaceHolder holder, int format, int width, int height) {
            super.onSurfaceChanged(holder, format, width, height);
            if (listener != null) listener.onSurfaceChanged(holder, width, height);
        }

        @Override
        public void onSurfaceDestroyed(SurfaceHolder holder) {
            if (listener != null) listener.onSurfaceDestroyed(holder);
            super.onSurfaceDestroyed(holder);
        }

        @Override
        public void onDestroy() {
            if (listener != null) listener.onDestroy();
            listener = null;
            super.onDestroy();
        }
    }
}
