namespace Speckle.Sdk.SQLite;

internal static class SqliteExceptions
{
  public static readonly IReadOnlyDictionary<int, string> SqliteErrorCodes = new Dictionary<int, string>
  {
    { 0, "Successful result" },
    { 1, "Generic error" },
    { 2, "Internal logic error in SQLite" },
    { 3, "Access permission denied" },
    { 4, "Callback routine requested an abort" },
    { 5, "The database file is locked" },
    { 6, "A table in the database is locked" },
    { 7, "A malloc() failed" },
    { 8, "Attempt to write a readonly database" },
    { 9, "Operation terminated by sqlite3_interrupt()" },
    { 10, "Some kind of disk I/O error occurred" },
    { 11, "The database disk image is malformed" },
    { 12, "Unknown opcode in sqlite3_file_control()" },
    { 13, "Insertion failed because database is full" },
    { 14, "Unable to open the database file" },
    { 15, "Database lock protocol error" },
    { 16, "Internal use only" },
    { 17, "The database schema changed" },
    { 18, "String or BLOB exceeds size limit" },
    { 19, "Abort due to constraint violation" },
    { 20, "Data type mismatch" },
    { 21, "Library used incorrectly" },
    { 22, "Uses OS features not supported on host" },
    { 23, "Authorization denied" },
    { 24, "Not used" },
    { 25, "2nd parameter to sqlite3_bind out of range" },
    { 26, "File opened that is not a database file" },
    { 27, "Notifications from sqlite3_log()" },
    { 28, "Warnings from sqlite3_log()" },
    { 100, "sqlite3_step() has another row ready" },
    { 101, "sqlite3_step() has finished executing" },
  };

  public static readonly IReadOnlyDictionary<int, string> SqliteExtendedResultCodes = new Dictionary<int, string>()
  {
    // Successful result codes
    { 0, "SQLITE_OK: Successful result" },
    // General error
    { 1, "SQLITE_ERROR: Generic error" },
    // I/O errors
    { 10, "SQLITE_IOERR: Some kind of disk I/O error occurred" },
    { 266, "SQLITE_IOERR_READ: I/O error during read operation" },
    { 267, "SQLITE_IOERR_SHORT_READ: I/O error: short read" },
    { 778, "SQLITE_IOERR_WRITE: I/O error during write operation" },
    { 1034, "SQLITE_IOERR_FSYNC: I/O error during fsync()" },
    { 1290, "SQLITE_IOERR_DIR_FSYNC: I/O error during directory fsync()" },
    { 1546, "SQLITE_IOERR_TRUNCATE: I/O error during file truncate" },
    { 1802, "SQLITE_IOERR_FSTAT: I/O error during file stat" },
    { 2058, "SQLITE_IOERR_UNLOCK: I/O error during file unlock" },
    { 2314, "SQLITE_IOERR_RDLOCK: I/O error during read lock" },
    { 2570, "SQLITE_IOERR_DELETE: I/O error during file delete" },
    { 2826, "SQLITE_IOERR_BLOCKED: I/O error while blocked on lock" },
    { 3338, "SQLITE_IOERR_NOMEM: I/O error due to malloc failure" },
    { 3594, "SQLITE_IOERR_ACCESS: I/O error during access check" },
    { 3850, "SQLITE_IOERR_CHECKRESERVEDLOCK: I/O error during reserved lock check" },
    { 4106, "SQLITE_IOERR_LOCK: I/O error when acquiring lock" },
    { 4362, "SQLITE_IOERR_CLOSE: I/O error during file close" },
    { 4618, "SQLITE_IOERR_DIR_CLOSE: I/O error when closing dir" },
    { 4874, "SQLITE_IOERR_SHMOPEN: I/O error during shared memory open" },
    { 5130, "SQLITE_IOERR_SHMSIZE: I/O error during shared memory sizing" },
    { 5642, "SQLITE_IOERR_SEEK: I/O error during seek" },
    { 5898, "SQLITE_IOERR_DELETE_NOENT: I/O error attempting to delete file" },
    { 6154, "SQLITE_IOERR_MMAP: I/O error during memory map" },
    { 6410, "SQLITE_IOERR_GETTEMPPATH: I/O error during temp path creation" },
    { 6922, "SQLITE_IOERR_CONVPATH: I/O error converting path" },
    { 7178, "SQLITE_IOERR_VNODE: I/O error in virtual node" },
    { 7426, "SQLITE_IOERR_AUTH: I/O error due to authorization failure" },
    // Corrupt database errors
    { 11, "SQLITE_CORRUPT: The database disk image is malformed" },
    { 267, "SQLITE_CORRUPT_VTAB: Column is corrupt in virtual table" },
    // Read-only errors
    { 8, "SQLITE_READONLY: Attempt to write to a readonly database" },
    { 264, "SQLITE_READONLY_RECOVERY: Access denied due to recovery mode" },
    { 520, "SQLITE_READONLY_CANTLOCK: Could not lock the database" },
    { 776, "SQLITE_READONLY_ROLLBACK: Read-only during rollback operation" },
    { 1032, "SQLITE_READONLY_DBMOVED: Database file moved, causing read-only error" },
    // Constraint errors
    { 19, "SQLITE_CONSTRAINT: Constraint violation" },
    { 275, "SQLITE_CONSTRAINT_UNIQUE: UNIQUE constraint failed" },
    { 531, "SQLITE_CONSTRAINT_FOREIGNKEY: FOREIGN KEY constraint failed" },
    { 787, "SQLITE_CONSTRAINT_NOTNULL: NOT NULL constraint failed" },
    { 1043, "SQLITE_CONSTRAINT_CHECK: CHECK constraint failed" },
    { 1299, "SQLITE_CONSTRAINT_PRIMARYKEY: PRIMARY KEY constraint failed" },
    { 1555, "SQLITE_CONSTRAINT_TRIGGER: Trigger caused constraint failure" },
    { 1803, "SQLITE_CONSTRAINT_ROWID: Row ID constraint violated" },
    { 2067, "SQLITE_CONSTRAINT_PINNED: Pinned constraint violation" },
    // CANTOPEN errors
    { 14, "SQLITE_CANTOPEN: Cannot open database file" },
    { 1038, "SQLITE_CANTOPEN_NOTEMPDIR: Unable to open temporary directory" },
    { 1542, "SQLITE_CANTOPEN_ISDIR: Cannot open database file as it is a directory" },
    { 1806, "SQLITE_CANTOPEN_FULLPATH: Full filepath cannot be accessed" },
    { 4102, "SQLITE_CANTOPEN_CONVPATH: Cannot open database file due to conversion path error" },
    // Other
    { 26, "SQLITE_NOTADB: File opened is not a database file" },
    { 100, "SQLITE_ROW: sqlite3_step() has another row ready" },
    { 101, "SQLITE_DONE: sqlite3_step() has finished executing" },
  };
}
