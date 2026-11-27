// Alien Skies for macOS: the ScreenSaverView that the system loads from "Alien Skies.saver".
//
// All astronomy and drawing live in AlienSkiesCore.dylib, the shared C# scene, settings and SkiaSharp
// renderer compiled with NativeAOT (see ../Exports.cs). This file only:
//   * loads that library from the bundle's Frameworks folder,
//   * asks it to draw each frame into a pixel buffer and puts the buffer on screen,
//   * builds the Options sheet from the settings description the library returns.
//
// Build with ../build-saver.sh (on a Mac with Xcode or the Command Line Tools).

#import <ScreenSaver/ScreenSaver.h>
#import <Cocoa/Cocoa.h>
#import <dlfcn.h>

// ---- the C API of AlienSkiesCore.dylib ---------------------------------------------------------------

typedef int (*as_init_fn)(const char *resourceDir, const char *libDir);
typedef intptr_t (*as_view_create_fn)(int screenIndex, int isPreview);
typedef int (*as_view_render_fn)(intptr_t view, uint8_t *pixels, int width, int height, int stride, float density);
typedef void (*as_view_destroy_fn)(intptr_t view);
typedef char *(*as_string_fn)(void);
typedef int (*as_settings_apply_fn)(const char *json);
typedef void (*as_void_fn)(void);
typedef void (*as_free_fn)(void *p);

static struct {
    BOOL tried, ok;
    NSString *error;
    as_init_fn init;
    as_view_create_fn viewCreate;
    as_view_render_fn viewRender;
    as_view_destroy_fn viewDestroy;
    as_string_fn settingsSchema, settingsPath, aboutText, lastError;
    as_settings_apply_fn settingsApply;
    as_void_fn settingsReload;
    as_free_fn release;
} Core;

static NSString *CoreLastError(void) {
    const char *e = Core.lastError ? Core.lastError() : NULL;
    return e ? [NSString stringWithUTF8String:e] : @"unknown error";
}

/// Loads AlienSkiesCore.dylib once per process. Several views (one per screen, plus previews) share it.
static BOOL LoadCore(NSBundle *bundle) {
    if (Core.tried) return Core.ok;
    Core.tried = YES;

    NSString *frameworks = bundle.privateFrameworksPath;
    NSString *path = [frameworks stringByAppendingPathComponent:@"AlienSkiesCore.dylib"];
    void *lib = dlopen(path.fileSystemRepresentation, RTLD_NOW | RTLD_LOCAL);
    if (!lib) {
        Core.error = [NSString stringWithFormat:@"Could not load %@: %s", path, dlerror()];
        return NO;
    }

#define BIND(field, type, name)                                                         \
    Core.field = (type)dlsym(lib, name);                                                \
    if (!Core.field) {                                                                  \
        Core.error = [NSString stringWithFormat:@"AlienSkiesCore.dylib lacks %s", name]; \
        return NO;                                                                      \
    }
    BIND(init, as_init_fn, "as_init")
    BIND(viewCreate, as_view_create_fn, "as_view_create")
    BIND(viewRender, as_view_render_fn, "as_view_render")
    BIND(viewDestroy, as_view_destroy_fn, "as_view_destroy")
    BIND(settingsSchema, as_string_fn, "as_settings_schema")
    BIND(settingsPath, as_string_fn, "as_settings_path")
    BIND(aboutText, as_string_fn, "as_about_text")
    BIND(lastError, as_string_fn, "as_last_error")
    BIND(settingsApply, as_settings_apply_fn, "as_settings_apply")
    BIND(settingsReload, as_void_fn, "as_settings_reload")
    BIND(release, as_free_fn, "as_free")
#undef BIND

    if (Core.init(bundle.resourcePath.fileSystemRepresentation, frameworks.fileSystemRepresentation) != 0) {
        Core.error = CoreLastError();
        return NO;
    }
    Core.ok = YES;
    return YES;
}

/// Takes ownership of a string returned by the library.
static NSString *TakeString(char *s) {
    if (!s) return nil;
    NSString *result = [NSString stringWithUTF8String:s];
    Core.release(s);
    return result;
}

/// Top-down layout for the Options form. (Class names are prefixed: every saver shares one process.)
@interface AlienSkiesFlippedView : NSView
@end

@implementation AlienSkiesFlippedView
- (BOOL)isFlipped {
    return YES;
}
@end

// ---- the view ----------------------------------------------------------------------------------------

@interface AlienSkiesView : ScreenSaverView
@end

@implementation AlienSkiesView {
    intptr_t _view;
    uint8_t *_pixels;
    size_t _pixelsWidth, _pixelsHeight;
    CGContextRef _bitmap;
    NSString *_error;
    NSWindow *_sheet;
    NSMutableArray<NSDictionary *> *_fields; // {key, kind, control}
}

- (instancetype)initWithFrame:(NSRect)frame isPreview:(BOOL)isPreview {
    self = [super initWithFrame:frame isPreview:isPreview];
    if (self) {
        self.animationTimeInterval = 1.0 / 30.0;
        if (!LoadCore([NSBundle bundleForClass:[self class]])) _error = Core.error;

        // Since macOS 14 the host process (legacyScreenSaver) often keeps running, and keeps animating
        // hidden views, after the screen saver is dismissed. Quit when the system says it is stopping.
        if (!isPreview) {
            [NSDistributedNotificationCenter.defaultCenter addObserver:self
                                                              selector:@selector(screenSaverWillStop:)
                                                                  name:@"com.apple.screensaver.willstop"
                                                                object:nil];
        }
    }
    return self;
}

- (void)dealloc {
    [NSDistributedNotificationCenter.defaultCenter removeObserver:self];
    [self releaseFrame];
    if (_view && Core.ok) Core.viewDestroy(_view);
}

- (void)screenSaverWillStop:(NSNotification *)note {
    [self stopAnimation];
    dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(0.5 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
        exit(0);
    });
}

/// Screens counted left to right, so a panorama continues across monitors the same way as on Windows.
- (int)screenIndex {
    NSScreen *mine = self.window.screen;
    if (!mine) return 0;
    NSArray<NSScreen *> *sorted = [NSScreen.screens sortedArrayUsingComparator:^NSComparisonResult(NSScreen *a, NSScreen *b) {
        if (a.frame.origin.x != b.frame.origin.x) return a.frame.origin.x < b.frame.origin.x ? NSOrderedAscending : NSOrderedDescending;
        return a.frame.origin.y > b.frame.origin.y ? NSOrderedAscending : NSOrderedDescending; // top first
    }];
    NSUInteger i = [sorted indexOfObject:mine];
    return i == NSNotFound ? 0 : (int)i;
}

- (void)startAnimation {
    [super startAnimation];
    if (!_view && Core.ok) {
        _view = Core.viewCreate(self.screenIndex, self.isPreview ? 1 : 0);
        if (!_view) _error = CoreLastError();
    }
}

- (void)releaseFrame {
    if (_bitmap) CGContextRelease(_bitmap);
    _bitmap = NULL;
    free(_pixels);
    _pixels = NULL;
    _pixelsWidth = _pixelsHeight = 0;
}

- (void)animateOneFrame {
    if (!_view || _error) {
        [self setNeedsDisplay:YES];
        return;
    }

    NSRect backing = [self convertRectToBacking:self.bounds];
    size_t w = (size_t)MAX(1, lround(backing.size.width)), h = (size_t)MAX(1, lround(backing.size.height));
    if (w != _pixelsWidth || h != _pixelsHeight) {
        [self releaseFrame];
        _pixels = calloc(w * 4, h);
        CGColorSpaceRef rgb = CGColorSpaceCreateWithName(kCGColorSpaceSRGB);
        _bitmap = CGBitmapContextCreate(_pixels, w, h, 8, w * 4, rgb,
                                        kCGImageAlphaPremultipliedFirst | kCGBitmapByteOrder32Little); // = Skia BGRA premul
        CGColorSpaceRelease(rgb);
        _pixelsWidth = w;
        _pixelsHeight = h;
    }

    float density = (float)(backing.size.width / MAX(1.0, self.bounds.size.width));
    if (Core.viewRender(_view, _pixels, (int)w, (int)h, (int)(w * 4), density) != 0) _error = CoreLastError();
    [self setNeedsDisplay:YES];
}

- (void)drawRect:(NSRect)rect {
    CGContextRef ctx = NSGraphicsContext.currentContext.CGContext;
    if (_error) {
        [NSColor.blackColor setFill];
        NSRectFill(self.bounds);
        NSDictionary *attrs = @{NSForegroundColorAttributeName: NSColor.systemOrangeColor,
                                NSFontAttributeName: [NSFont systemFontOfSize:self.isPreview ? 9 : 14]};
        [[@"Alien Skies could not start.\n" stringByAppendingString:_error]
            drawInRect:NSInsetRect(self.bounds, 12, 12) withAttributes:attrs];
        return;
    }
    if (!_bitmap) {
        [NSColor.blackColor setFill];
        NSRectFill(self.bounds);
        return;
    }
    CGImageRef image = CGBitmapContextCreateImage(_bitmap);
    CGContextSetInterpolationQuality(ctx, kCGInterpolationNone);
    CGContextDrawImage(ctx, NSRectToCGRect(self.bounds), image);
    CGImageRelease(image);
}

- (BOOL)isOpaque {
    return YES;
}

// ---- Options sheet -----------------------------------------------------------------------------------
// Built from the library's description of SaverSettings, so it shows the same settings, labels and help
// text as the Windows settings dialog and the Android settings screen.

- (BOOL)hasConfigureSheet {
    return YES;
}

- (NSWindow *)configureSheet {
    if (!Core.ok) return nil;
    Core.settingsReload();
    NSString *json = TakeString(Core.settingsSchema());
    NSArray *schema = json ? [NSJSONSerialization JSONObjectWithData:[json dataUsingEncoding:NSUTF8StringEncoding] options:0 error:nil] : nil;
    if (![schema isKindOfClass:NSArray.class]) return nil;

    const CGFloat width = 560, margin = 20, labelWidth = 190, controlX = margin + labelWidth + 10;
    const CGFloat controlWidth = width - controlX - margin - 16;
    NSView *form = [[AlienSkiesFlippedView alloc] initWithFrame:NSMakeRect(0, 0, width, 100)];
    _fields = [NSMutableArray array];
    CGFloat y = margin;
    NSString *group = nil;

    for (NSDictionary *f in schema) {
        NSString *kind = f[@"kind"], *label = f[@"label"], *help = f[@"help"];
        id value = f[@"value"];
        if (![group isEqualToString:f[@"group"]]) {
            group = f[@"group"];
            NSTextField *heading = [NSTextField labelWithString:group];
            heading.font = [NSFont boldSystemFontOfSize:14];
            heading.frame = NSMakeRect(margin, y + 6, width - 2 * margin, 20);
            [form addSubview:heading];
            y += 34;
        }

        NSControl *control;
        if ([kind isEqualToString:@"bool"]) {
            NSButton *box = [NSButton checkboxWithTitle:label target:nil action:nil];
            box.state = [value boolValue] ? NSControlStateValueOn : NSControlStateValueOff;
            box.frame = NSMakeRect(controlX - 2, y, controlWidth, 20);
            control = box;
        } else {
            NSTextField *name = [NSTextField labelWithString:label];
            name.alignment = NSTextAlignmentRight;
            name.frame = NSMakeRect(margin, y + 3, labelWidth, 18);
            [form addSubview:name];

            NSArray *choices = f[@"choices"];
            if ([kind isEqualToString:@"choice"] && choices.count > 25) {
                NSComboBox *combo = [[NSComboBox alloc] initWithFrame:NSMakeRect(controlX, y, controlWidth, 24)];
                [combo addItemsWithObjectValues:choices];
                combo.completes = YES;
                combo.numberOfVisibleItems = 20;
                combo.stringValue = value ?: @"";
                control = combo;
            } else if ([kind isEqualToString:@"choice"]) {
                NSPopUpButton *popup = [[NSPopUpButton alloc] initWithFrame:NSMakeRect(controlX - 3, y - 2, controlWidth + 6, 26) pullsDown:NO];
                [popup addItemsWithTitles:choices];
                [popup selectItemWithTitle:value ?: @""];
                control = popup;
            } else {
                NSTextField *field = [NSTextField textFieldWithString:
                    [kind isEqualToString:@"number"] ? [NSString stringWithFormat:@"%g", [value doubleValue]] : (value ?: @"")];
                field.frame = NSMakeRect(controlX, y, [kind isEqualToString:@"number"] ? 120 : controlWidth, 22);
                control = field;
            }
        }
        [form addSubview:control];
        [_fields addObject:@{@"key": f[@"key"], @"kind": kind, @"control": control}];
        y += 28;

        if (help.length) {
            NSTextField *hint = [NSTextField wrappingLabelWithString:help];
            hint.font = [NSFont systemFontOfSize:11];
            hint.textColor = NSColor.secondaryLabelColor;
            hint.preferredMaxLayoutWidth = controlWidth;
            NSSize size = [hint sizeThatFits:NSMakeSize(controlWidth, CGFLOAT_MAX)];
            hint.frame = NSMakeRect(controlX, y - 4, controlWidth, ceil(size.height));
            [form addSubview:hint];
            y += ceil(size.height) + 6;
        }
    }
    NSTextField *note = [NSTextField wrappingLabelWithString:
        @"Imaginary planets are edited in the settings file for now (Open Settings File)."];
    note.font = [NSFont systemFontOfSize:11];
    note.textColor = NSColor.secondaryLabelColor;
    note.frame = NSMakeRect(margin, y + 8, width - 2 * margin, 30);
    [form addSubview:note];
    y += 40;

    // Copyright, GPL notice and credits (the same text as the Windows About box and the Android settings page).
    NSString *aboutString = TakeString(Core.aboutText());
    if (aboutString.length) {
        NSTextField *aboutHeading = [NSTextField labelWithString:@"About"];
        aboutHeading.font = [NSFont boldSystemFontOfSize:14];
        aboutHeading.frame = NSMakeRect(margin, y + 6, width - 2 * margin, 20);
        [form addSubview:aboutHeading];
        y += 32;
        NSTextField *about = [NSTextField wrappingLabelWithString:aboutString];
        about.font = [NSFont systemFontOfSize:11];
        about.textColor = NSColor.secondaryLabelColor;
        about.selectable = YES;
        about.preferredMaxLayoutWidth = width - 2 * margin;
        NSSize size = [about sizeThatFits:NSMakeSize(width - 2 * margin, CGFLOAT_MAX)];
        about.frame = NSMakeRect(margin, y, width - 2 * margin, ceil(size.height));
        [form addSubview:about];
        y += ceil(size.height);
    }
    y += margin;
    [form setFrameSize:NSMakeSize(width, y)];

    const CGFloat height = 620, buttonBar = 56;
    _sheet = [[NSWindow alloc] initWithContentRect:NSMakeRect(0, 0, width, height)
                                         styleMask:NSWindowStyleMaskTitled
                                           backing:NSBackingStoreBuffered
                                             defer:YES];
    _sheet.title = @"Alien Skies";
    NSView *content = _sheet.contentView;

    NSScrollView *scroll = [[NSScrollView alloc] initWithFrame:NSMakeRect(0, buttonBar, width, height - buttonBar)];
    scroll.hasVerticalScroller = YES;
    scroll.drawsBackground = NO;
    scroll.documentView = form;
    [form scrollPoint:NSZeroPoint];
    [content addSubview:scroll];

    NSButton *ok = [NSButton buttonWithTitle:@"OK" target:self action:@selector(sheetOK:)];
    ok.keyEquivalent = @"\r";
    ok.frame = NSMakeRect(width - margin - 90, 14, 90, 28);
    NSButton *cancel = [NSButton buttonWithTitle:@"Cancel" target:self action:@selector(sheetCancel:)];
    cancel.keyEquivalent = @"\033";
    cancel.frame = NSMakeRect(width - margin - 190, 14, 90, 28);
    NSButton *open = [NSButton buttonWithTitle:@"Open Settings File" target:self action:@selector(sheetOpenFile:)];
    open.frame = NSMakeRect(margin, 14, 160, 28);
    [content addSubview:ok];
    [content addSubview:cancel];
    [content addSubview:open];
    return _sheet;
}

- (void)closeSheet {
    NSWindow *sheet = _sheet;
    _sheet = nil;
    _fields = nil;
    if (sheet.sheetParent) [sheet.sheetParent endSheet:sheet];
    else [NSApp endSheet:sheet];
    [sheet orderOut:nil];
}

- (void)sheetOK:(id)sender {
    NSMutableDictionary *values = [NSMutableDictionary dictionary];
    for (NSDictionary *f in _fields) {
        NSString *kind = f[@"kind"];
        id control = f[@"control"];
        if ([kind isEqualToString:@"bool"]) values[f[@"key"]] = @([control state] == NSControlStateValueOn);
        else if ([kind isEqualToString:@"number"]) {
            NSString *text = [[control stringValue] stringByReplacingOccurrencesOfString:@"," withString:@"."];
            values[f[@"key"]] = @(text.doubleValue);
        } else if ([control isKindOfClass:NSPopUpButton.class]) values[f[@"key"]] = [control titleOfSelectedItem] ?: @"";
        else values[f[@"key"]] = [control stringValue] ?: @"";
    }
    NSData *json = [NSJSONSerialization dataWithJSONObject:values options:0 error:nil];
    NSString *text = [[NSString alloc] initWithData:json encoding:NSUTF8StringEncoding];
    if (Core.settingsApply(text.UTF8String) != 0) {
        NSAlert *alert = [[NSAlert alloc] init];
        alert.messageText = @"Alien Skies could not save its settings.";
        alert.informativeText = CoreLastError();
        [alert beginSheetModalForWindow:_sheet completionHandler:nil];
        return;
    }
    [self restartView];
    [self closeSheet];
}

- (void)sheetCancel:(id)sender {
    Core.settingsReload();
    [self closeSheet];
}

- (void)sheetOpenFile:(id)sender {
    NSString *path = TakeString(Core.settingsPath());
    if (path) [NSWorkspace.sharedWorkspace openURL:[NSURL fileURLWithPath:path]];
}

/// The preview behind the sheet picks up the new planet straight away.
- (void)restartView {
    if (_view) Core.viewDestroy(_view);
    _view = Core.viewCreate(self.screenIndex, self.isPreview ? 1 : 0);
    _error = _view ? nil : CoreLastError();
}

@end
