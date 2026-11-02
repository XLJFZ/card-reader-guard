#import <Cocoa/Cocoa.h>
#import <DiskArbitration/DiskArbitration.h>
#import <IOKit/IOKitLib.h>
#import <UserNotifications/UserNotifications.h>
#include <unistd.h>
#include "GuardPolicy.h"

static NSString *const BundleID = @"io.github.xljfz.CardReaderGuard";
static NSString *SupportPath(void) {
    return [NSHomeDirectory() stringByAppendingPathComponent:@"Library/Application Support/CardReaderGuard"];
}
static void Log(NSString *message) {
    NSString *directory = SupportPath();
    [[NSFileManager defaultManager] createDirectoryAtPath:directory withIntermediateDirectories:YES attributes:@{NSFilePosixPermissions:@0700} error:nil];
    NSString *path = [directory stringByAppendingPathComponent:@"status.log"];
    NSDictionary *attributes = [[NSFileManager defaultManager] attributesOfItemAtPath:path error:nil];
    if (!attributes || [attributes[NSFileSize] unsignedLongLongValue] > 262144) [NSData.data writeToFile:path atomically:YES];
    NSFileHandle *handle = [NSFileHandle fileHandleForWritingAtPath:path];
    [handle seekToEndOfFile];
    [handle writeData:[[NSString stringWithFormat:@"%@ %@\n", NSDate.date, message] dataUsingEncoding:NSUTF8StringEncoding]];
    [handle closeFile];
}
static BOOL Text(id value) { return [value isKindOfClass:NSString.class] && [value length] > 0; }
static BOOL ValidBindings(id bindings) {
    if (![bindings isKindOfClass:NSArray.class]) return NO;
    NSMutableSet *seen = NSMutableSet.set;
    for (id value in bindings) {
        if (![value isKindOfClass:NSDictionary.class]) return NO;
        NSDictionary *b = value;
        if (!Text(b[@"identity"]) || ![b[@"identity"] hasPrefix:@"usb:"] ||
            !Text(b[@"model"]) || !Text(b[@"name"]) || [b[@"name"] length] > 80 ||
            ![b[@"minutes"] isKindOfClass:NSNumber.class] ||
            [b[@"minutes"] doubleValue] != [b[@"minutes"] integerValue] ||
            [b[@"minutes"] integerValue] < 1 || [b[@"minutes"] integerValue] > 120 ||
            ![b[@"enabled"] isKindOfClass:NSNumber.class] || [seen containsObject:b[@"identity"]]) return NO;
        [seen addObject:b[@"identity"]];
    }
    return YES;
}
static NSMutableArray *MutableBindings(NSArray *source) {
    NSMutableArray *result = NSMutableArray.array;
    for (NSDictionary *binding in source) [result addObject:binding.mutableCopy];
    return result;
}
static id Property(io_registry_entry_t entry, NSString *key) {
    return CFBridgingRelease(IORegistryEntryCreateCFProperty(entry, (__bridge CFStringRef)key, kCFAllocatorDefault, 0));
}

@interface RGDevice : NSObject
@property(copy) NSString *identity, *model, *bsd;
@property(copy) NSArray<NSString *> *volumes;
@property uint64_t mediaID, size;
@property RGSample sample;
@end
@implementation RGDevice
@end

// Inspect only ancestors of the selected IOMedia. Stop at the first USB device;
// never use a hub's identity or controller-wide statistics as a substitute.
static NSDictionary *Hardware(io_registry_entry_t media) {
    io_registry_entry_t current = media;
    IOObjectRetain(current);
    NSDictionary *statistics = nil;
    NSDictionary *hardware = nil;
    for (unsigned depth = 0; current && depth < 32; depth++) {
        if (!statistics && IOObjectConformsTo(current, "IOBlockStorageDriver")) {
            id value = Property(current, @"Statistics");
            if ([value isKindOfClass:NSDictionary.class]) statistics = value;
        }
        if (IOObjectConformsTo(current, "IOUSBHostDevice") || IOObjectConformsTo(current, "IOUSBDevice")) {
            id serial = Property(current, @"USB Serial Number");
            id vendor = Property(current, @"idVendor");
            id product = Property(current, @"idProduct");
            id location = Property(current, @"locationID");
            id deviceClass = Property(current, @"bDeviceClass");
            id name = Property(current, @"USB Product Name");
            if (Text(serial) && [vendor isKindOfClass:NSNumber.class] && [product isKindOfClass:NSNumber.class] &&
                [location isKindOfClass:NSNumber.class] && [location unsignedIntValue] != 0 &&
                [deviceClass isKindOfClass:NSNumber.class] && [deviceClass unsignedIntValue] != 9) {
                NSString *identity = [NSString stringWithFormat:@"usb:%04x:%04x:%@:%08x", [vendor unsignedIntValue], [product unsignedIntValue], serial, [location unsignedIntValue]];
                hardware = @{@"identity":identity, @"model":Text(name) ? name : @"USB 存储设备", @"statistics":statistics ?: @{}};
            }
            break;
        }
        io_registry_entry_t parent = IO_OBJECT_NULL;
        kern_return_t status = IORegistryEntryGetParentEntry(current, kIOServicePlane, &parent);
        IOObjectRelease(current);
        current = status == KERN_SUCCESS ? parent : IO_OBJECT_NULL;
    }
    if (current) IOObjectRelease(current);
    return hardware;
}
static BOOL SampleFromDictionary(NSDictionary *statistics, RGSample *sample) {
    NSArray *keys = @[@"Bytes (Read)", @"Bytes (Write)", @"Operations (Read)", @"Operations (Write)"];
    uint64_t values[4];
    for (NSUInteger i = 0; i < keys.count; i++) {
        id value = statistics[keys[i]];
        if (![value isKindOfClass:NSNumber.class] || [value doubleValue] < 0) return NO;
        values[i] = [value unsignedLongLongValue];
    }
    *sample = (RGSample){values[0], values[1], values[2], values[3]};
    return YES;
}
static BOOL ProtectedContent(NSString *content) {
    return [content localizedCaseInsensitiveContainsString:@"APFS"] ||
           [content caseInsensitiveCompare:@"7C3457EF-0000-11AA-AA11-00306543ECAC"] == NSOrderedSame;
}
static BOOL PathOnVolume(NSString *path, NSString *volume) {
    if (!Text(path) || !Text(volume)) return NO;
    return [path isEqualToString:volume] || [path hasPrefix:[volume stringByAppendingString:@"/"]];
}
static NSArray<RGDevice *> *Devices(DASessionRef session) {
    io_iterator_t iterator = IO_OBJECT_NULL;
    if (IOServiceGetMatchingServices(kIOMainPortDefault, IOServiceMatching("IOMedia"), &iterator) != KERN_SUCCESS) return @[];
    NSMutableArray<NSDictionary *> *records = NSMutableArray.array;
    io_registry_entry_t media;
    while ((media = IOIteratorNext(iterator))) {
        DADiskRef disk = DADiskCreateFromIOMedia(kCFAllocatorDefault, session, media);
        if (disk) {
            NSDictionary *description = CFBridgingRelease(DADiskCopyDescription(disk));
            DADiskRef whole = DADiskCopyWholeDisk(disk);
            const char *name = DADiskGetBSDName(disk);
            const char *wholeName = whole ? DADiskGetBSDName(whole) : NULL;
            uint64_t registryID = 0;
            IORegistryEntryGetRegistryEntryID(media, &registryID);
            if (description && name && wholeName) {
                NSMutableDictionary *record = [@{@"description":description, @"bsd":@(name), @"wholeBSD":@(wholeName), @"mediaID":@(registryID)} mutableCopy];
                if ([description[(__bridge NSString *)kDADiskDescriptionMediaWholeKey] boolValue]) {
                    NSDictionary *hardware = Hardware(media);
                    if (hardware) record[@"hardware"] = hardware;
                }
                [records addObject:record];
            }
            if (whole) CFRelease(whole);
            CFRelease(disk);
        }
        IOObjectRelease(media);
    }
    IOObjectRelease(iterator);
    NSMutableArray<RGDevice *> *candidates = NSMutableArray.array;
    NSMutableDictionary<NSString *, NSNumber *> *identityCounts = NSMutableDictionary.dictionary;
    NSString *appPath = NSBundle.mainBundle.bundlePath.stringByResolvingSymlinksInPath;
    NSString *configPath = SupportPath().stringByResolvingSymlinksInPath;
    for (NSDictionary *record in records) {
        NSDictionary *description = record[@"description"], *hardware = record[@"hardware"];
        if (!hardware) continue;
        NSString *identity = hardware[@"identity"];
        identityCounts[identity] = @([identityCounts[identity] unsignedIntegerValue] + 1);
        id internal = description[(__bridge NSString *)kDADiskDescriptionDeviceInternalKey];
        NSString *protocol = description[(__bridge NSString *)kDADiskDescriptionDeviceProtocolKey];
        if (![internal isKindOfClass:NSNumber.class] || [internal boolValue] ||
            ![protocol isKindOfClass:NSString.class] || [protocol caseInsensitiveCompare:@"USB"] != NSOrderedSame) continue;
        BOOL protected = NO;
        NSMutableArray *volumes = NSMutableArray.array;
        for (NSDictionary *child in records) {
            if (![child[@"wholeBSD"] isEqualToString:record[@"bsd"]]) continue;
            NSDictionary *details = child[@"description"];
            NSString *content = details[(__bridge NSString *)kDADiskDescriptionMediaContentKey] ?: @"";
            if (ProtectedContent(content)) protected = YES;
            NSURL *url = details[(__bridge NSString *)kDADiskDescriptionVolumePathKey];
            if ([url isKindOfClass:NSURL.class] && url.isFileURL) {
                NSString *volume = url.path.stringByResolvingSymlinksInPath;
                if ([volume isEqualToString:@"/"] || PathOnVolume(appPath, volume) || PathOnVolume(configPath, volume)) protected = YES;
                [volumes addObject:volume];
            }
        }
        RGSample sample;
        NSNumber *size = description[(__bridge NSString *)kDADiskDescriptionMediaSizeKey];
        if (protected || ![record[@"mediaID"] unsignedLongLongValue] || ![size isKindOfClass:NSNumber.class] || !size.unsignedLongLongValue ||
            !SampleFromDictionary(hardware[@"statistics"], &sample)) continue;
        RGDevice *device = RGDevice.new;
        device.identity = identity; device.model = hardware[@"model"]; device.bsd = record[@"bsd"];
        device.mediaID = [record[@"mediaID"] unsignedLongLongValue]; device.size = size.unsignedLongLongValue;
        device.sample = sample; device.volumes = volumes;
        [candidates addObject:device];
    }
    NSIndexSet *duplicates = [candidates indexesOfObjectsPassingTest:^BOOL(RGDevice *device, NSUInteger index, BOOL *stop) {
        (void)index; (void)stop;
        return [identityCounts[device.identity] unsignedIntegerValue] != 1;
    }];
    [candidates removeObjectsAtIndexes:duplicates];
    return candidates;
}

@class RGApp;
@interface RGOperation : NSObject
@property(weak) RGApp *owner;
@property(strong) RGDevice *device;
@property(copy) NSString *name;
@property DADiskRef disk;
@end
@implementation RGOperation
- (void)dealloc { if (_disk) CFRelease(_disk); }
@end

@interface RGApp : NSObject <NSApplicationDelegate, NSWindowDelegate, NSTableViewDataSource, NSTableViewDelegate, NSTextFieldDelegate, UNUserNotificationCenterDelegate>
@property DASessionRef session;
@property(strong) NSMutableArray<NSMutableDictionary *> *bindings, *draft;
@property(strong) NSMutableDictionary<NSString *, NSValue *> *states;
@property(strong) NSArray<RGDevice *> *connected;
@property(strong) NSWindow *window;
@property(strong) NSTableView *table;
@property(strong) NSTextField *nameField, *minutesField, *detail;
@property(strong) NSButton *enabledButton, *startupButton;
@property BOOL managing, ejecting;
- (void)showManager:(id)sender;
- (void)tick:(NSTimer *)timer;
- (void)finishOperation:(RGOperation *)operation dissenter:(DADissenterRef)dissenter;
@end

static void Ejected(DADiskRef disk, DADissenterRef dissenter, void *context) {
    (void)disk;
    RGOperation *operation = CFBridgingRelease(context);
    [operation.owner finishOperation:operation dissenter:dissenter];
}
static void Unmounted(DADiskRef disk, DADissenterRef dissenter, void *context) {
    (void)disk;
    RGOperation *operation = (__bridge RGOperation *)context;
    if (dissenter) { Ejected(disk, dissenter, context); return; }
    BOOL same = NO;
    for (RGDevice *fresh in Devices(operation.owner.session)) {
        if ([fresh.identity isEqualToString:operation.device.identity] && [fresh.bsd isEqualToString:operation.device.bsd] && fresh.mediaID == operation.device.mediaID) same = YES;
    }
    if (!same) {
        RGOperation *retained = CFBridgingRelease(context);
        retained.owner.ejecting = NO;
        [retained.owner.states removeObjectForKey:retained.device.identity];
        Log(@"SKIP identity changed after unmount; no eject requested");
        return;
    }
    DADiskEject(operation.disk, kDADiskEjectOptionDefault, Ejected, context);
}
static NSTextField *Label(NSString *title, NSRect frame, CGFloat size, BOOL bold) {
    NSTextField *label = [NSTextField labelWithString:title];
    label.frame = frame; label.font = bold ? [NSFont boldSystemFontOfSize:size] : [NSFont systemFontOfSize:size];
    label.lineBreakMode = NSLineBreakByTruncatingMiddle;
    return label;
}
static NSButton *Button(NSString *title, NSRect frame, id target, SEL action) {
    NSButton *button = [NSButton buttonWithTitle:title target:target action:action];
    button.frame = frame; button.bezelStyle = NSBezelStyleRounded;
    return button;
}

@implementation RGApp
- (NSString *)configPath { return [SupportPath() stringByAppendingPathComponent:@"devices.json"]; }
- (NSString *)agentPath { return [NSHomeDirectory() stringByAppendingPathComponent:[NSString stringWithFormat:@"Library/LaunchAgents/%@.plist", BundleID]]; }
- (void)applicationDidFinishLaunching:(NSNotification *)notification {
    (void)notification;
    self.session = DASessionCreate(kCFAllocatorDefault);
    if (!self.session) { Log(@"START FAILED: Disk Arbitration session unavailable"); [NSApp terminate:nil]; return; }
    DASessionScheduleWithRunLoop(self.session, CFRunLoopGetMain(), kCFRunLoopDefaultMode);
    self.states = NSMutableDictionary.dictionary;
    self.bindings = NSMutableArray.array;
    NSData *data = [NSData dataWithContentsOfFile:self.configPath];
    if (data) {
        id parsed = [NSJSONSerialization JSONObjectWithData:data options:NSJSONReadingMutableContainers error:nil];
        if (!ValidBindings(parsed)) {
            Log(@"CONFIG ERROR: original file preserved");
            if (![NSProcessInfo.processInfo.arguments containsObject:@"--background"]) {
                NSAlert *alert = NSAlert.new; alert.messageText = @"无法读取绑定配置"; alert.informativeText = @"原文件已保留。请检查 Application Support/CardReaderGuard/devices.json。"; [alert runModal];
            }
            [NSApp terminate:nil]; return;
        }
        self.bindings = MutableBindings(parsed);
    }
    BOOL background = [NSProcessInfo.processInfo.arguments containsObject:@"--background"];
    if (background && !data) { [NSApp terminate:nil]; return; }
    NSMenu *menu = NSMenu.new;
    NSMenuItem *application = NSMenuItem.new; application.submenu = NSMenu.new;
    NSMenuItem *manage = [[NSMenuItem alloc] initWithTitle:@"管理读卡器" action:@selector(showManager:) keyEquivalent:@","]; manage.target = self;
    [application.submenu addItem:manage];
    [application.submenu addItem:[NSMenuItem separatorItem]];
    [application.submenu addItem:[[NSMenuItem alloc] initWithTitle:@"退出监测" action:@selector(terminate:) keyEquivalent:@"q"]];
    [menu addItem:application]; NSApp.mainMenu = menu;
    [[NSWorkspace sharedWorkspace].notificationCenter addObserver:self selector:@selector(woke:) name:NSWorkspaceDidWakeNotification object:nil];
    NSTimer *timer = [NSTimer timerWithTimeInterval:5 target:self selector:@selector(tick:) userInfo:nil repeats:YES];
    [NSRunLoop.mainRunLoop addTimer:timer forMode:NSRunLoopCommonModes];
    Log([NSString stringWithFormat:@"START macOS preview bindings=%lu", (unsigned long)self.bindings.count]);
    if (!background) [self showManager:nil];
}
- (BOOL)applicationShouldHandleReopen:(NSApplication *)sender hasVisibleWindows:(BOOL)flag {
    (void)sender; (void)flag; [self showManager:nil]; return YES;
}
- (BOOL)applicationShouldTerminateAfterLastWindowClosed:(NSApplication *)sender { (void)sender; return NO; }
- (void)woke:(NSNotification *)notification { (void)notification; [self.states removeAllObjects]; }
- (void)tick:(NSTimer *)timer {
    (void)timer;
    if (self.managing || self.ejecting) return;
    NSArray<RGDevice *> *devices = Devices(self.session);
    for (NSDictionary *binding in self.bindings) {
        NSString *identity = binding[@"identity"];
        if (![binding[@"enabled"] boolValue]) { [self.states removeObjectForKey:identity]; continue; }
        RGDevice *device = nil;
        for (RGDevice *candidate in devices) if ([candidate.identity isEqualToString:identity] && [candidate.model isEqualToString:binding[@"model"]]) device = candidate;
        if (!device) { [self.states removeObjectForKey:identity]; continue; }
        RGIdle state = {0};
        [self.states[identity] getValue:&state size:sizeof(state)];
        BOOL ready = RGObserve(&state, device.sample, device.mediaID, NSProcessInfo.processInfo.systemUptime, [binding[@"minutes"] unsignedIntValue], true);
        self.states[identity] = [NSValue valueWithBytes:&state objCType:@encode(RGIdle)];
        if (!ready) continue;
        RGDevice *fresh = nil;
        for (RGDevice *candidate in Devices(self.session)) if ([candidate.identity isEqualToString:identity] && [candidate.model isEqualToString:binding[@"model"]] && candidate.mediaID == device.mediaID && [candidate.bsd isEqualToString:device.bsd] && RGSampleEqual(candidate.sample, device.sample)) fresh = candidate;
        if (!fresh) { [self.states removeObjectForKey:identity]; continue; }
        DADiskRef disk = DADiskCreateFromBSDName(kCFAllocatorDefault, self.session, fresh.bsd.UTF8String);
        if (!disk) { [self.states removeObjectForKey:identity]; continue; }
        RGOperation *operation = RGOperation.new; operation.owner = self; operation.device = fresh; operation.name = binding[@"name"]; operation.disk = disk;
        self.ejecting = YES;
        // Whole-device unmount is requested without the force option. A denial
        // never advances to eject and never closes another application's files.
        DADiskUnmount(disk, kDADiskUnmountOptionWhole, Unmounted, (void *)CFBridgingRetain(operation));
        return;
    }
}
- (void)finishOperation:(RGOperation *)operation dissenter:(DADissenterRef)dissenter {
    self.ejecting = NO;
    [self.states removeObjectForKey:operation.device.identity];
    if (dissenter) { Log([NSString stringWithFormat:@"REFUSED result=%d", DADissenterGetStatus(dissenter)]); return; }
    Log(@"EJECT SUCCESS (whole target media)");
    UNMutableNotificationContent *content = UNMutableNotificationContent.new;
    content.title = @"读卡器已安全弹出";
    content.body = [NSString stringWithFormat:@"%@ 的目标介质已安全弹出，再次使用可能需要重新插拔。", operation.name];
    UNNotificationRequest *request = [UNNotificationRequest requestWithIdentifier:NSUUID.UUID.UUIDString content:content trigger:nil];
    [UNUserNotificationCenter.currentNotificationCenter addNotificationRequest:request withCompletionHandler:^(NSError *error) { if (error) Log(@"Success notification unavailable; check notification permission"); }];
}
- (void)buildWindow {
    self.window = [[NSWindow alloc] initWithContentRect:NSMakeRect(0,0,960,610) styleMask:NSWindowStyleMaskTitled|NSWindowStyleMaskClosable|NSWindowStyleMaskMiniaturizable backing:NSBackingStoreBuffered defer:NO];
    self.window.title = @"读卡器管理 · macOS 测试版"; self.window.delegate = self; self.window.releasedWhenClosed = NO;
    NSView *view = self.window.contentView;
    [view addSubview:Label(@"读卡器管理", NSMakeRect(28,542,650,36), 25, YES)];
    [view addSubview:Label(@"只监测你确认过的 USB 设备。管理期间暂停弹出，关闭后重新计时。", NSMakeRect(29,510,880,25), 12, NO)];
    NSScrollView *scroll = [[NSScrollView alloc] initWithFrame:NSMakeRect(28,215,650,272)];
    self.table = [[NSTableView alloc] initWithFrame:scroll.bounds]; self.table.rowHeight = 45;
    NSArray *columns = @[@[@"name",@"名称",@180],@[@"model",@"设备型号",@220],@[@"minutes",@"空闲时间",@85],@[@"status",@"状态",@150]];
    for (NSArray *spec in columns) { NSTableColumn *column = [[NSTableColumn alloc] initWithIdentifier:spec[0]]; column.title = spec[1]; column.width = [spec[2] doubleValue]; [self.table addTableColumn:column]; }
    self.table.dataSource = self; self.table.delegate = self; self.table.allowsMultipleSelection = NO;
    scroll.documentView = self.table; scroll.hasVerticalScroller = YES; scroll.borderType = NSBezelBorder; [view addSubview:scroll];
    [view addSubview:Label(@"设备名称",NSMakeRect(702,453,230,24),12,YES)];
    self.nameField = [[NSTextField alloc] initWithFrame:NSMakeRect(702,417,230,27)]; self.nameField.delegate = self; [view addSubview:self.nameField];
    [view addSubview:Label(@"没有读写后多久弹出（1–120 分钟）",NSMakeRect(702,370,230,25),11,NO)];
    self.minutesField = [[NSTextField alloc] initWithFrame:NSMakeRect(702,332,90,27)];
    NSNumberFormatter *formatter = NSNumberFormatter.new; formatter.minimum = @1; formatter.maximum = @120; formatter.allowsFloats = NO; self.minutesField.formatter = formatter; self.minutesField.delegate = self; [view addSubview:self.minutesField];
    self.enabledButton = [NSButton checkboxWithTitle:@"启用这只读卡器" target:self action:@selector(updateEditor:)]; self.enabledButton.frame = NSMakeRect(702,286,230,28); [view addSubview:self.enabledButton];
    [view addSubview:Button(@"更换设备",NSMakeRect(698,229,114,34),self,@selector(replaceDevice:))];
    [view addSubview:Button(@"删除绑定",NSMakeRect(818,229,114,34),self,@selector(removeDevice:))];
    self.detail = Label(@"",NSMakeRect(28,151,904,51),11,NO); self.detail.maximumNumberOfLines = 2; self.detail.textColor = NSColor.secondaryLabelColor; [view addSubview:self.detail];
    self.startupButton = [NSButton checkboxWithTitle:@"登录 macOS 后自动启动" target:nil action:nil]; self.startupButton.frame = NSMakeRect(28,102,450,28); [view addSubview:self.startupButton];
    [view addSubview:Label(@"成功提示需要通知权限；安全弹出不保证 USB 断电或降温。",NSMakeRect(28,72,900,22),11,NO)];
    [view addSubview:Button(@"添加读卡器",NSMakeRect(28,24,125,34),self,@selector(addDevice:))];
    [view addSubview:Button(@"刷新连接状态",NSMakeRect(162,24,130,34),self,@selector(refresh:))];
    [view addSubview:Button(@"退出监测",NSMakeRect(303,24,110,34),NSApp,@selector(terminate:))];
    [view addSubview:Button(@"取消更改",NSMakeRect(648,24,125,34),self,@selector(cancelManager:))];
    NSButton *save = Button(@"保存并静默运行",NSMakeRect(786,24,146,34),self,@selector(save:)); save.keyEquivalent = @"\r"; [view addSubview:save];
    [self.window center];
}
- (void)showManager:(id)sender {
    (void)sender;
    if (self.managing) { [self.window makeKeyAndOrderFront:nil]; [NSApp activateIgnoringOtherApps:YES]; return; }
    self.managing = YES; self.draft = MutableBindings(self.bindings);
    if (!self.window) [self buildWindow];
    self.startupButton.state = [[NSFileManager defaultManager] fileExistsAtPath:self.agentPath] ? NSControlStateValueOn : NSControlStateValueOff;
    [self refresh:nil]; [self.window makeKeyAndOrderFront:nil]; [NSApp activateIgnoringOtherApps:YES];
}
- (void)refresh:(id)sender {
    (void)sender; self.connected = Devices(self.session); [self.table reloadData];
    if (self.draft.count && self.table.selectedRow < 0) [self.table selectRowIndexes:[NSIndexSet indexSetWithIndex:0] byExtendingSelection:NO];
    [self tableViewSelectionDidChange:nil];
}
- (NSInteger)numberOfRowsInTableView:(NSTableView *)tableView { (void)tableView; return (NSInteger)self.draft.count; }
- (NSView *)tableView:(NSTableView *)tableView viewForTableColumn:(NSTableColumn *)column row:(NSInteger)row {
    (void)tableView;
    NSDictionary *binding = self.draft[(NSUInteger)row]; NSString *text;
    if ([column.identifier isEqualToString:@"minutes"]) text = [NSString stringWithFormat:@"%@ 分钟",binding[@"minutes"]];
    else if ([column.identifier isEqualToString:@"status"]) {
        BOOL online = NO; for (RGDevice *device in self.connected) if ([device.identity isEqualToString:binding[@"identity"]] && [device.model isEqualToString:binding[@"model"]]) online = YES;
        text = ![binding[@"enabled"] boolValue] ? @"已暂停" : online ? @"已连接" : @"未连接或不可监测";
    } else text = binding[column.identifier] ?: @"";
    return Label(text,NSMakeRect(8,0,column.width-12,24),12,NO);
}
- (NSMutableDictionary *)selectedBinding {
    NSInteger row = self.table.selectedRow;
    return row >= 0 && (NSUInteger)row < self.draft.count ? self.draft[(NSUInteger)row] : nil;
}
- (void)tableViewSelectionDidChange:(NSNotification *)notification {
    (void)notification;
    NSDictionary *binding = self.selectedBinding;
    self.nameField.enabled = binding != nil; self.minutesField.enabled = binding != nil; self.enabledButton.enabled = binding != nil;
    self.nameField.stringValue = binding[@"name"] ?: @""; self.minutesField.stringValue = binding ? [binding[@"minutes"] stringValue] : @"5";
    self.enabledButton.state = [binding[@"enabled"] boolValue] ? NSControlStateValueOn : NSControlStateValueOff;
    self.detail.stringValue = binding ? [NSString stringWithFormat:@"设备：%@\nUSB 身份：%@",binding[@"model"],binding[@"identity"]] : @"尚未绑定读卡器。请插入读卡器和卡，点击“添加读卡器”。";
}
- (void)updateEditor:(id)sender {
    (void)sender;
    NSMutableDictionary *binding = self.selectedBinding; if (!binding) return;
    binding[@"name"] = self.nameField.stringValue; binding[@"minutes"] = @(self.minutesField.integerValue); binding[@"enabled"] = @(self.enabledButton.state == NSControlStateValueOn);
    [self.table reloadData];
}
- (void)controlTextDidEndEditing:(NSNotification *)notification { (void)notification; [self updateEditor:nil]; }
- (RGDevice *)pickDevice {
    NSArray<RGDevice *> *devices = Devices(self.session);
    NSAlert *alert = NSAlert.new; alert.messageText = @"确认要自动弹出的读卡器";
    if (!devices.count) { alert.informativeText = @"没有找到可监测的设备。请插入 USB 读卡器和存储卡后重试。无可核对序列号、缺少读写统计、多介质及 APFS 设备暂不支持。"; [alert runModal]; return nil; }
    alert.informativeText = @"列表也可能包含移动硬盘，请核对型号、容量和盘符。每次新增或更换都需确认。";
    [alert addButtonWithTitle:@"确认此读卡器"]; [alert addButtonWithTitle:@"取消"];
    NSView *accessory = [[NSView alloc] initWithFrame:NSMakeRect(0,0,540,91)];
    NSPopUpButton *choice = [[NSPopUpButton alloc] initWithFrame:NSMakeRect(0,55,540,28) pullsDown:NO];
    [choice addItemWithTitle:@"请选择设备（不会默认绑定）"];
    for (RGDevice *device in devices) [choice addItemWithTitle:[NSString stringWithFormat:@"%@ · %.1f GB · %@ · %@",device.model,(double)device.size/1e9,device.bsd,[device.volumes componentsJoinedByString:@", "]]];
    NSButton *confirmed = [NSButton checkboxWithTitle:@"我确认选中的是读卡器，并同意按设定空闲时间安全弹出。" target:nil action:nil]; confirmed.frame = NSMakeRect(0,9,540,31);
    [accessory addSubview:choice]; [accessory addSubview:confirmed]; alert.accessoryView = accessory;
    if ([alert runModal] != NSAlertFirstButtonReturn) return nil;
    if (choice.indexOfSelectedItem <= 0 || confirmed.state != NSControlStateValueOn) { NSAlert *missing = NSAlert.new; missing.messageText = @"尚未确认设备"; missing.informativeText = @"未保存任何新绑定。请重新添加并明确选择、勾选确认。"; [missing runModal]; return nil; }
    RGDevice *chosen = devices[(NSUInteger)choice.indexOfSelectedItem-1];
    for (RGDevice *fresh in Devices(self.session)) if ([fresh.identity isEqualToString:chosen.identity] && fresh.mediaID == chosen.mediaID && [fresh.bsd isEqualToString:chosen.bsd] && [fresh.model isEqualToString:chosen.model]) return fresh;
    NSAlert *changed = NSAlert.new; changed.messageText = @"设备已变化，请重新选择"; [changed runModal]; return nil;
}
- (void)addDevice:(id)sender {
    (void)sender; RGDevice *device = self.pickDevice; if (!device) return;
    for (NSDictionary *binding in self.draft) if ([binding[@"identity"] isEqualToString:device.identity]) { NSAlert *alert = NSAlert.new; alert.messageText = @"这只设备已经添加，请直接修改已有绑定。"; [alert runModal]; return; }
    [self.draft addObject:[@{@"identity":device.identity,@"model":device.model,@"name":device.model,@"minutes":@5,@"enabled":@YES} mutableCopy]];
    [self refresh:nil]; [self.table selectRowIndexes:[NSIndexSet indexSetWithIndex:self.draft.count-1] byExtendingSelection:NO];
}
- (void)replaceDevice:(id)sender {
    (void)sender; NSMutableDictionary *old = self.selectedBinding; if (!old) return;
    RGDevice *device = self.pickDevice; if (!device) return;
    for (NSDictionary *binding in self.draft) if (binding != old && [binding[@"identity"] isEqualToString:device.identity]) { NSAlert *alert = NSAlert.new; alert.messageText = @"目标设备已有绑定，不能重复添加。"; [alert runModal]; return; }
    old[@"identity"] = device.identity; old[@"model"] = device.model; [self refresh:nil];
}
- (void)removeDevice:(id)sender {
    (void)sender; NSMutableDictionary *binding = self.selectedBinding; if (!binding) return;
    NSAlert *alert = NSAlert.new; alert.messageText = @"删除这只读卡器的绑定？"; alert.informativeText = @"保存后将不再自动弹出它，不会删除卡内文件。"; [alert addButtonWithTitle:@"取消"]; [alert addButtonWithTitle:@"删除绑定"];
    if ([alert runModal] == NSAlertSecondButtonReturn) { [self.draft removeObjectIdenticalTo:binding]; [self refresh:nil]; }
}
- (BOOL)saveStartup:(NSError **)error {
    NSFileManager *manager = NSFileManager.defaultManager;
    if (self.startupButton.state != NSControlStateValueOn) {
        return ![manager fileExistsAtPath:self.agentPath] || [manager removeItemAtPath:self.agentPath error:error];
    }
    if (![manager createDirectoryAtPath:self.agentPath.stringByDeletingLastPathComponent withIntermediateDirectories:YES attributes:nil error:error]) return NO;
    NSDictionary *agent = @{@"Label":BundleID,@"ProgramArguments":@[NSBundle.mainBundle.executablePath,@"--background"],@"RunAtLoad":@YES,@"KeepAlive":@NO,@"ProcessType":@"Background"};
    NSData *plist = [NSPropertyListSerialization dataWithPropertyList:agent format:NSPropertyListXMLFormat_v1_0 options:0 error:error];
    return plist && [plist writeToFile:self.agentPath options:NSDataWritingAtomic error:error];
}
- (void)save:(id)sender {
    (void)sender; [self.window makeFirstResponder:nil]; [self updateEditor:nil];
    if (!ValidBindings(self.draft)) { NSAlert *alert = NSAlert.new; alert.messageText = @"请检查设备名称、空闲时间或重复绑定"; alert.informativeText = @"名称需为 1–80 个字符，空闲时间为 1–120 的整数分钟。同一设备只能保存一次。"; [alert runModal]; return; }
    NSError *error = nil;
    [[NSFileManager defaultManager] createDirectoryAtPath:SupportPath() withIntermediateDirectories:YES attributes:@{NSFilePosixPermissions:@0700} error:&error];
    NSData *data = [NSJSONSerialization dataWithJSONObject:self.draft options:NSJSONWritingPrettyPrinted error:&error];
    if (!data || ![data writeToFile:self.configPath options:NSDataWritingAtomic error:&error]) { NSAlert *alert = NSAlert.new; alert.messageText = @"未能保存绑定"; alert.informativeText = error.localizedDescription ?: @"请检查文件权限。"; [alert runModal]; return; }
    self.bindings = MutableBindings(self.draft);
    if (![self saveStartup:&error]) { NSAlert *alert = NSAlert.new; alert.messageText = @"绑定已保存，但登录启动设置未能更新"; alert.informativeText = error.localizedDescription ?: @"可稍后重试。"; [alert runModal]; }
    UNUserNotificationCenter.currentNotificationCenter.delegate = self;
    [UNUserNotificationCenter.currentNotificationCenter requestAuthorizationWithOptions:UNAuthorizationOptionAlert completionHandler:^(BOOL granted, NSError *notificationError) { (void)notificationError; if (!granted) Log(@"Notification permission not granted; monitoring remains active"); }];
    [self closeManager];
}
- (void)closeManager { self.managing = NO; [self.states removeAllObjects]; [self.window orderOut:nil]; }
- (void)cancelManager:(id)sender { (void)sender; [self closeManager]; }
- (BOOL)windowShouldClose:(NSWindow *)sender { (void)sender; [self closeManager]; return NO; }
- (void)userNotificationCenter:(UNUserNotificationCenter *)center willPresentNotification:(UNNotification *)notification withCompletionHandler:(void (^)(UNNotificationPresentationOptions))completionHandler {
    (void)center; (void)notification; completionHandler(UNNotificationPresentationOptionBanner|UNNotificationPresentationOptionList);
}
- (void)dealloc { if (_session) CFRelease(_session); }
@end

static int SelfTest(void) {
    unsigned tests = 0;
#define CHECK(value) do { if (!(value)) { fprintf(stderr,"FAIL line %d\n",__LINE__); return 2; } tests++; } while (0)
    RGSample sample = {10,20,1,2}; RGIdle first = {0}, other = {0};
    CHECK(!RGObserve(&first,sample,1,0,5,true));
    for (unsigned i=1;i<60;i++) CHECK(!RGObserve(&first,sample,1,i*5,5,true));
    CHECK(RGObserve(&first,sample,1,300,5,true));
    sample.bytesRead++; CHECK(!RGObserve(&first,sample,1,305,5,true));
    sample.bytesWritten++; CHECK(!RGObserve(&first,sample,1,310,5,true));
    sample.reads++; CHECK(!RGObserve(&first,sample,1,315,5,true));
    sample.writes++; CHECK(!RGObserve(&first,sample,1,320,5,true));
    CHECK(!RGObserve(&first,sample,2,325,5,true));
    CHECK(!RGObserve(&first,sample,2,1000,5,true));
    CHECK(first.idleSince==1000);
    CHECK(!RGObserve(&first,sample,2,990,5,true));
    CHECK(!RGObserve(&first,sample,2,995,5,false) && !first.hasBaseline);
    CHECK(!RGObserve(&other,sample,3,100,1,true)); RGReset(&first); CHECK(other.hasBaseline);
    CHECK(!RGObserve(&first,sample,1,0,0,true));
    CHECK(!RGObserve(&first,sample,1,0,121,true));
    RGSample parsed; CHECK(!SampleFromDictionary(@{},&parsed));
    CHECK(SampleFromDictionary(@{@"Bytes (Read)":@10,@"Bytes (Write)":@20,@"Operations (Read)":@1,@"Operations (Write)":@2},&parsed));
    CHECK(RGSampleEqual(parsed,(RGSample){10,20,1,2}));
    CHECK(ProtectedContent(@"Apple_APFS")); CHECK(ProtectedContent(@"7C3457EF-0000-11AA-AA11-00306543ECAC"));
    CHECK(!ProtectedContent(@"Microsoft Basic Data"));
    CHECK(PathOnVolume(@"/Volumes/CARD/App.app",@"/Volumes/CARD"));
    CHECK(!PathOnVolume(@"/Volumes/CARD2/App.app",@"/Volumes/CARD"));
    NSDictionary *a = @{@"identity":@"usb:0001:0002:A:1234",@"name":@"Reader A",@"model":@"Model",@"minutes":@5,@"enabled":@YES};
    NSMutableDictionary *b = a.mutableCopy; b[@"identity"] = @"usb:0001:0002:B:5678"; b[@"minutes"] = @12; b[@"enabled"] = @NO;
    CHECK(ValidBindings(@[a,b])); CHECK(!ValidBindings(@[a,a]));
    NSMutableArray *copy = MutableBindings(@[a,b]); copy[0][@"name"] = @"Draft"; CHECK([a[@"name"] isEqualToString:@"Reader A"]);
    NSData *json = [NSJSONSerialization dataWithJSONObject:@[a,b] options:0 error:nil];
    NSArray *loaded = [NSJSONSerialization JSONObjectWithData:json options:NSJSONReadingMutableContainers error:nil];
    CHECK(ValidBindings(loaded) && [loaded[1][@"minutes"] intValue]==12 && ![loaded[1][@"enabled"] boolValue]);
    [copy removeObjectAtIndex:0]; CHECK(copy.count==1 && ValidBindings(copy));
    [copy removeAllObjects]; CHECK(ValidBindings(copy));
    b[@"minutes"] = @0; CHECK(!ValidBindings(@[b]));
    b[@"minutes"] = @1.5; CHECK(!ValidBindings(@[b]));
    b[@"minutes"] = @5; b[@"identity"] = @"disk2"; CHECK(!ValidBindings(@[b]));
    printf("PASS %u macOS safety checks; no unmount or eject requests issued.\n", tests);
    return 0;
#undef CHECK
}
int main(int argc, const char *argv[]) {
    @autoreleasepool {
        for (int i=1;i<argc;i++) {
            if (strcmp(argv[i],"--self-test")==0) return SelfTest();
            if (strcmp(argv[i],"--diagnostics")==0) {
                DASessionRef session = DASessionCreate(kCFAllocatorDefault);
                if (!session) return 2;
                NSArray *devices = Devices(session); printf("Supported external USB candidates: %lu\n",(unsigned long)devices.count); CFRelease(session); return 0;
            }
        }
        NSApplication *application = NSApplication.sharedApplication;
        [application setActivationPolicy:NSApplicationActivationPolicyAccessory];
        static RGApp *delegate; delegate = RGApp.new; application.delegate = delegate;
        [application run];
    }
    return 0;
}
