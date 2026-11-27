// Smoke test for AlienSkiesCore: loads the NativeAOT library the way the .saver does (dlopen + dlsym),
// reads and changes settings, renders one frame per simulated screen and checks it isn't blank.
// Usage: smoke <path to AlienSkiesCore.dylib|.so> <resource dir> <lib dir> [out.ppm]
// Exit code 0 = pass. Run with HOME pointed at a scratch folder so it doesn't touch real settings.
#include <dlfcn.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

typedef int (*init_fn)(const char *, const char *);
typedef intptr_t (*create_fn)(int, int);
typedef int (*render_fn)(intptr_t, uint8_t *, int, int, int, float);
typedef void (*destroy_fn)(intptr_t);
typedef char *(*str_fn)(void);
typedef int (*apply_fn)(const char *);
typedef void (*free_fn)(void *);

static void *lib;
static void *sym(const char *name) {
    void *p = dlsym(lib, name);
    if (!p) { fprintf(stderr, "FAIL: missing export %s\n", name); exit(2); }
    return p;
}

int main(int argc, char **argv) {
    if (argc < 4) { fprintf(stderr, "usage: smoke <lib> <resource dir> <lib dir> [out.ppm]\n"); return 2; }
    lib = dlopen(argv[1], RTLD_NOW | RTLD_LOCAL);
    if (!lib) { fprintf(stderr, "FAIL: dlopen: %s\n", dlerror()); return 2; }

    init_fn init = (init_fn)sym("as_init");
    create_fn create = (create_fn)sym("as_view_create");
    render_fn render = (render_fn)sym("as_view_render");
    destroy_fn destroy = (destroy_fn)sym("as_view_destroy");
    str_fn schema = (str_fn)sym("as_settings_schema");
    str_fn path = (str_fn)sym("as_settings_path");
    str_fn last_error = (str_fn)sym("as_last_error");
    apply_fn apply = (apply_fn)sym("as_settings_apply");
    free_fn release = (free_fn)sym("as_free");
    sym("as_settings_reload");
    str_fn about = (str_fn)sym("as_about_text");

    if (init(argv[2], argv[3]) != 0) { fprintf(stderr, "FAIL: as_init: %s\n", last_error()); return 1; }

    char *notice = about();
    if (!notice || !strstr(notice, "GNU General Public License") || !strstr(notice, "Certification Tool and Die")) {
        fprintf(stderr, "FAIL: as_about_text lacks the license notice: %s\n", notice ? notice : last_error());
        return 1;
    }
    release(notice);

    char *json = schema();
    if (!json) { fprintf(stderr, "FAIL: as_settings_schema: %s\n", last_error()); return 1; }
    if (!strstr(json, "\"PlanetName\"") || !strstr(json, "TRAPPIST-1 e") || !strstr(json, "\"TidallyLocked\"")) {
        fprintf(stderr, "FAIL: schema lacks the planet list or enum choices:\n%.2000s\n", json);
        return 1;
    }
    printf("schema: %zu bytes\n", strlen(json));
    release(json);

    if (apply("{\"PlanetName\":\"TRAPPIST-1 e\",\"LatitudeDeg\":12.5,\"RotationMode\":\"Free\",\"ShowInfo\":true}") != 0) {
        fprintf(stderr, "FAIL: as_settings_apply: %s\n", last_error());
        return 1;
    }
    json = schema();
    if (!strstr(json, "\"value\":\"TRAPPIST-1 e\"") || !strstr(json, "\"value\":12.5") || !strstr(json, "\"value\":\"Free\"")) {
        fprintf(stderr, "FAIL: applied settings did not stick:\n%.3000s\n", json);
        return 1;
    }
    release(json);

    char *file = path();
    if (!file) { fprintf(stderr, "FAIL: as_settings_path: %s\n", last_error()); return 1; }
    FILE *f = fopen(file, "r");
    if (!f) { fprintf(stderr, "FAIL: settings file %s was not written\n", file); return 1; }
    fclose(f);
    printf("settings: %s\n", file);
    release(file);

    const int w = 1280, h = 720, stride = w * 4;
    uint8_t *pixels = calloc((size_t)stride, h);
    for (int screen = 0; screen < 2; screen++) {
        intptr_t view = create(screen, 0);
        if (!view) { fprintf(stderr, "FAIL: as_view_create: %s\n", last_error()); return 1; }
        for (int frame = 0; frame < 3; frame++) {
            if (render(view, pixels, w, h, stride, 2.0f) != 0) {
                fprintf(stderr, "FAIL: as_view_render: %s\n", last_error());
                return 1;
            }
        }
        long lit = 0;
        for (long i = 0; i < (long)w * h; i++) {
            uint8_t *p = pixels + i * 4;
            if (p[0] + p[1] + p[2] > 120) lit++;
        }
        printf("screen %d: %ld bright pixels\n", screen, lit);
        if (lit < 50) { fprintf(stderr, "FAIL: frame looks blank\n"); return 1; }
        if (screen == 0 && argc > 4) {
            FILE *out = fopen(argv[4], "wb");
            fprintf(out, "P6\n%d %d\n255\n", w, h);
            for (long i = 0; i < (long)w * h; i++) {
                uint8_t *p = pixels + i * 4; // BGRA
                fputc(p[2], out); fputc(p[1], out); fputc(p[0], out);
            }
            fclose(out);
        }
        destroy(view);
    }
    free(pixels);
    printf("PASS\n");
    return 0;
}
