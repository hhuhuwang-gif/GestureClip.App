CREATE TABLE IF NOT EXISTS Settings (
    Key TEXT PRIMARY KEY,
    Value TEXT,
    ValueType TEXT NOT NULL DEFAULT 'string',
    UpdatedAt TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS AppBlacklist (
    Id TEXT PRIMARY KEY,
    ProcessName TEXT NOT NULL UNIQUE,
    Reason TEXT,
    BlockClipboard INTEGER NOT NULL DEFAULT 1,
    BlockGesture INTEGER NOT NULL DEFAULT 0,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS IX_AppBlacklist_ProcessName
ON AppBlacklist (ProcessName);

CREATE TABLE IF NOT EXISTS ClipboardItems (
    Id TEXT PRIMARY KEY,
    ContentType TEXT NOT NULL,
    TextContent TEXT,
    PreviewText TEXT,
    Hash TEXT NOT NULL,
    PlainTextHash TEXT,
    SourceApp TEXT,
    SourceProcess TEXT,
    IsPinned INTEGER NOT NULL DEFAULT 0,
    IsFavorite INTEGER NOT NULL DEFAULT 0,
    IsSensitive INTEGER NOT NULL DEFAULT 0,
    UseCount INTEGER NOT NULL DEFAULT 0,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL,
    LastUsedAt TEXT
);

CREATE INDEX IF NOT EXISTS IX_ClipboardItems_CreatedAt
ON ClipboardItems (CreatedAt DESC);

CREATE INDEX IF NOT EXISTS IX_ClipboardItems_Hash
ON ClipboardItems (Hash);

CREATE INDEX IF NOT EXISTS IX_ClipboardItems_PlainTextHash
ON ClipboardItems (PlainTextHash);

CREATE INDEX IF NOT EXISTS IX_ClipboardItems_Pinned_CreatedAt
ON ClipboardItems (IsPinned DESC, CreatedAt DESC);

CREATE TABLE IF NOT EXISTS Actions (
    Id TEXT PRIMARY KEY,
    Name TEXT NOT NULL,
    ActionType TEXT NOT NULL,
    IsBuiltIn INTEGER NOT NULL DEFAULT 1,
    IsEnabled INTEGER NOT NULL DEFAULT 1,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS ActionParameters (
    Id TEXT PRIMARY KEY,
    ActionId TEXT NOT NULL,
    ParamKey TEXT NOT NULL,
    ParamValue TEXT,
    FOREIGN KEY (ActionId) REFERENCES Actions(Id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS IX_ActionParameters_ActionId
ON ActionParameters (ActionId);

CREATE TABLE IF NOT EXISTS GestureRules (
    Id TEXT PRIMARY KEY,
    Name TEXT NOT NULL,
    Pattern TEXT NOT NULL UNIQUE,
    ActionId TEXT NOT NULL,
    IsEnabled INTEGER NOT NULL DEFAULT 1,
    IsDefault INTEGER NOT NULL DEFAULT 0,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL,
    FOREIGN KEY (ActionId) REFERENCES Actions(Id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS IX_GestureRules_Pattern
ON GestureRules (Pattern);

CREATE TABLE IF NOT EXISTS UsageLogs (
    Id TEXT PRIMARY KEY,
    EventType TEXT NOT NULL,
    ActionId TEXT,
    GesturePattern TEXT,
    SourceApp TEXT,
    SourceProcess TEXT,
    Message TEXT,
    CreatedAt TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS IX_UsageLogs_CreatedAt
ON UsageLogs (CreatedAt DESC);
