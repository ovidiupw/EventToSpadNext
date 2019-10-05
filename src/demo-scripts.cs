using SPAD.neXt.Interfaces;
using SPAD.neXt.Interfaces.Configuration;
using SPAD.neXt.Interfaces.Events;
using SPAD.neXt.Interfaces.Scripting;
using System.IO;
using System;
using Utility;
using Events;

/// <summary>
/// Defines the name of the namespace that will be used by SPAD Next in the provided variable's name.
/// The namespace name will be used as a prefix to the variable name. The variable name will be set to
/// the provider class name, which is defined in this namespace.
/// The final SPAD Next variable name will be of the following format: {namespace_name}{scripting_class_name}.
/// </summary>
namespace DeckToSpadNext
{

    /// <summary>
    /// DO NOT USE directly. Used as a namespace-level mix-in so that the value providers regularly
    /// provide updates instead of getting stuck at the first provided value.
    /// 
    /// This class can control the refresh interval.static See the VALUE_SCAN_INTERVAL_MILLIS field.
    /// </summary>
    public class DO_NOT_USE_AS_PROVIDER_ValueRefresher : IScriptValueProvider
    {
        /// <summary>
        /// DO NOT USE directly. This is only need at the namespace level inside the SPAD Next scripting 
        /// namespace so that the scripting providers are regularly refreshed (checked for new/changed values).
        /// </summary>
        public void RefreshRegularly()
        {
            (EventSystem.GetDataDefinition("LOCAL:LOCAL TIME")?.GetValue()).GetValueOrDefault(0);
        }

        public double ProvideValue(IApplication app)
        {
            RefreshRegularly();
            throw new System.NotImplementedException(@"This value proivder must not be used.
            It only exists so that the 'RefreshRegularly' function can be implemented. This
            must throw an exeception to function properly");
        }
    }

    /// <summary>
    /// SPAD Next value provider that fetches and parses demo events and provides the resulting value to SPAD Next
    /// according to the SPAD Next scripting interface: https://github.com/c0nnex/SPAD.neXt/wiki/Scripting-Interface.
    /// 
    /// The name of the class will be included in the SPAD Next variable name.
    /// The final SPAD Next variable name will be of the following format: {namespace_name}{scripting_class_name}. 
    /// </summary>
    public class DemoProvider : IScriptValueProvider
    {
        private static readonly string DATA_PATH = @"C:\Users\Ovidiu\Desktop\EventsToSpadNext";
        private static readonly string OUTPUT_PATH = $@"{DATA_PATH}\output";
        private static readonly string RESOURCES_PATH = $@"{DATA_PATH}\resources";
        private static readonly string EVENTS_PATH = $@"{RESOURCES_PATH}\events";

        private static readonly string LOGS_PATH = $@"{OUTPUT_PATH}\logs";
        private static FileLogger LOG = FileLogger.GetLogger(LOGS_PATH, typeof(DemoProvider), LogLevel.Info);

        private static readonly string INPUTS_PATH = $@"{EVENTS_PATH}\demo\inputs";
        private static readonly string ACTIVE_VALUE_PATH = $@"{EVENTS_PATH}\demo\active-value\active.val";

        private static readonly string OFF_VALUE_PATH = $@"{EVENTS_PATH}\demo\values\off.val";
        private string offValue = File.ReadAllText(OFF_VALUE_PATH).Replace(Environment.NewLine, "");

        private static readonly string ON_VALUE_PATH = $@"{EVENTS_PATH}\demo\values\on.val";
        private string onValue = File.ReadAllText(ON_VALUE_PATH).Replace(Environment.NewLine, "");

        private FileContentConsumer eventConsumer;

        /// <summary>
        /// Constructs a new instance of the DemoProvider class. Due to the fact that SPAD Next will instantiate this class,
        /// this constructor cannot have any arguments because SPAD Next always calls the default constructor (no arguments).
        /// </summary>
        public DemoProvider()
        {
            eventConsumer = new FileContentConsumer
            .Builder()
            .WithActiveValueFile(ACTIVE_VALUE_PATH)
            .WithDefaultValue(offValue)
            .WithInputsDir(INPUTS_PATH)
            .WithLogger(LOG)
            .Build();
        }

        /// <summary>
        /// Documented in SPAD Next: https://github.com/c0nnex/SPAD.neXt/wiki/Scripting-Interface.
        /// </summary>
        public double ProvideValue(IApplication app)
        {
            try
            {
                string eventValue = eventConsumer.Consume();
                LOG.Info($"Consumed event was: '{eventValue}'");
                return EventToSpadOutput(eventValue);
            }
            catch
            {
                LOG.Info("Could not consume event");
                return EventToSpadOutput();
            }
        }

        private double EventToSpadOutput()
        {
            return EventToSpadOutput(null);
        }

        private double EventToSpadOutput(string input)
        {
            if (input == null)
            {
                LOG.Info($"No event input detected. Falling back to active event: '{eventConsumer.GetActiveValue()}'");
                input = eventConsumer.GetActiveValue();
            }

            if (input.Equals(onValue))
            {
                return 1;
            }
            else if (input.Equals(offValue))
            {
                return 0;
            }
            throw new InvalidOperationException($"Could not match event '{input}' to any known event");
        }
    }
}

/// <summary>
/// Encapsulates Data Access Objects for interacting with external events 
/// (e.g. a button press on an Elgato Stream Deck device).
/// </summary>
namespace Events
{

    /// <summary>
    /// Specializes in event consumption from files on disk. An event is defined as a file created in a
    /// directory monitored by the consumer. The event data is all the content in the event file.
    /// Once an event file is consumed, the file is deleted in order to prevent re-consumption.
    /// Once re-consumption has been prevented, the consumed event's data is persisted as the active
    /// event data in a directory monitored by the consumer. The active event data is persisted so that
    /// when the consumer doesn't have anything to consume it still can provide valid event data.
    /// </summary>
    public class FileContentConsumer
    {
        private static readonly string EVENT_INPUT_FILE_FORMAT = "*.in";

        private FileLogger logger;
        private string inputsDir;
        private string activeValueFile;
        private string defaultValue;

        /// <summary>
        /// Builds a new instace of a FileContentConsumer. See the documentation for each particular
        /// field in order to understand its semantics.
        /// </summary>
        public class Builder
        {
            /// <summary>
            /// A logger used to record the actions of the consumer.
            /// </summary>
            private FileLogger logger;

            /// <summary>
            /// Path to the directory where the input file for each event is stored.
            /// All files with the '.in' extension are considered to be inputs.
            /// This directory must exist prior to running the consumer to consume events.
            /// However, it is not required that the directory contains actual event files.
            /// </summary>
            private string inputsDir;

            /// <summary>
            /// Path to the file where the latest consumed event's data will be stored. 
            /// This file does not need to be created prior to consuming events. If the file does not
            /// exist, it will be created by the consumer on demand.
            /// </summary>
            private string activeValueFile;

            /// <summary>
            /// Value that is used in case there are no events to be consumed AND there is no data
            /// recorded in the active value file. This can be viewed as the initial state of the event.
            /// </summary>
            private string defaultValue;

            public FileContentConsumer Build()
            {
                FileContentConsumer consumer = new FileContentConsumer();
                consumer.logger = logger;
                consumer.inputsDir = inputsDir;
                consumer.activeValueFile = activeValueFile;
                consumer.defaultValue = defaultValue;
                return consumer;
            }

            public Builder WithLogger(FileLogger logger)
            {
                this.logger = logger;
                return this;
            }

            public Builder WithInputsDir(string inputsDir)
            {
                this.inputsDir = inputsDir;
                return this;
            }

            public Builder WithActiveValueFile(string activeValueFile)
            {
                this.activeValueFile = activeValueFile;
                return this;
            }

            public Builder WithDefaultValue(string defaultValue)
            {
                this.defaultValue = defaultValue;
                return this;
            }
        }

        /// <summary>
        /// Consumes the oldest event from the list of event files in the monitored directory. The age of an
        /// event is determined by the creation date of the input file of the event.
        /// </summary>
        /// <returns>All the contents of the input file of the event (i.e. the event data).</returns>
        public string Consume()
        {
            logger.Info("Consuming event");
            string latestInputFile = GetOldestInputFile();

            if (latestInputFile == null)
            {
                logger.Info("No event input was found");
                return null;
            }

            string latestInput = File.ReadAllText(latestInputFile).Replace(Environment.NewLine, "");
            logger.Info($"Consumed event successfully. Event was: '{latestInput}'");

            logger.Info($"Deleting event input file to prevent re-consumption: '{latestInputFile}'");
            File.Delete(latestInputFile);

            logger.Info($"Setting latest event input as the active event: '{latestInput}'");
            SetActiveValue(latestInput);

            return latestInput;
        }

        /// <summary>
        /// Loads the active event's data from the monitored file. The event data is the data 
        /// of the latest consumed event.
        /// </summary>
        /// <returns>All the contents of the active event's input file (i.e. the event data).</returns>
        public string GetActiveValue()
        {
            if (!File.Exists(activeValueFile))
            {
                return defaultValue;
            }
            string fileContents = File.ReadAllText(activeValueFile).Replace(Environment.NewLine, "");
            if (fileContents == null || fileContents.Length == 0)
            {
                return defaultValue;
            }
            return fileContents;
        }

        private void SetActiveValue(string value)
        {
            if (!File.Exists(activeValueFile))
            {
                logger.Info($"Active event file does not exist. Creating it at: '{activeValueFile}'");
                Directory.CreateDirectory(Path.GetDirectoryName(activeValueFile));
                File.Create(activeValueFile).Close();
            }
            File.WriteAllText(activeValueFile, value);
        }

        private string GetOldestInputFile()
        {
            logger.Info("Getting input file for oldest event");

            if (!Directory.Exists(inputsDir))
            {
                logger.Critical($"Event inputs directory at path '{inputsDir}' does not exist");
                throw new System.InvalidOperationException(
                    $"Expected event inputs directory at path '{inputsDir}' to exist but it did not exist");
            }

            logger.Info($"Listing event files in inputs directory '{inputsDir}'");
            string[] inputFiles = Directory.GetFiles(inputsDir, EVENT_INPUT_FILE_FORMAT, SearchOption.TopDirectoryOnly);

            if (inputFiles.Length == 0)
            {
                logger.Info("No event input file was found");
                return null;
            }

            logger.Info($"Found '{inputFiles.Length}' event input files. Finding oldest one to process");

            string latestInput = inputFiles[0];
            foreach (string currentFile in inputFiles)
            {
                if (DateTime.Compare(File.GetCreationTime(
                    currentFile), File.GetCreationTime(latestInput)) < 0)
                {
                    latestInput = currentFile;
                }
            }

            logger.Info($"Found oldest event input file: '{latestInput}'");
            return latestInput;
        }
    }
}

/// <summary>
/// Contains utility functionality and cross-cutting-concerns helpers.
/// </summary>
namespace Utility
{
    /// <summary>
    /// Identfies the threshold for logging. The threshold is used in order to determine
    /// when a log statement should actually be executed. Examples:
    /// 
    /// LogLevel set to Info -> Info level = 0 (integer) -> log.Info executed, log.Critical executed
    /// LogLevel set to Critical -> Critical level = 100 (integer) -> log.Info not executed, but log.Critical executed
    /// </summary>
    public static class LogLevel
    {
        public static int Info = 0;
        public static int Critical = 100;
    }

    /// <summary>
    /// Logs statements to files on disk.
    /// </summary>
    public class FileLogger
    {
        private static readonly int MAX_LOG_FILE_SIZE_MEGABYTES = 25;
        private static readonly int MAX_LOG_FILE_RETENTION_TIME_HOURS = 12;

        private string logsFilePath;
        private Type type;
        private int logLevel;

        /// <summary>
        /// Constructs a new instance of a file logger. The name of the log files will have the following format:
        /// 
        /// {this.type}.log
        /// 
        /// where {this.type} is the type configurred for the logger instance.
        /// 
        /// The log file will be cleared when either of the following is true:
        /// * The log file size eceeds 1000 megabytes;
        /// * The log file creation time is more than 12 hours ago.
        /// 
        /// </summary>
        /// <param name="logsDir">The directory in which the log files should be created.</param>
        /// <param name="type">The type of the class which owns and invokes the logger.</param>
        /// <param name="logLevel">The level of the logging. See the LogLevel class documentation for details.</param>
        /// <returns></returns>
        public static FileLogger GetLogger(string logsDir, Type type, int logLevel)
        {
            return new FileLogger($@"{logsDir}\{type}.log", type, logLevel);
        }

        /// <summary>
        /// Appends the supplied message to the logging file that is owned by the current instance of the logger.
        /// Prefixes the message with the time current (seconds precision) and the [INFO] qualifier.
        /// </summary>
        /// <param name="message">The message to be logged to the file.</param>
        public void Info(string message)
        {
            if (logLevel > LogLevel.Info)
            {
                return;
            }
            File.AppendAllLines(logsFilePath, new[] { $@"[{DateTime.UtcNow}] [{type}] [INFO] {message}" });
        }

        /// <summary>
        /// Appends the supplied message to the logging file that is owned by the current instance of the logger.
        /// Prefixes the message with the time current (seconds precision) and the [CRITICAL] qualifier.
        /// </summary>
        /// <param name="message">The message to be logged to the file.</param>
        public void Critical(string message)
        {
            if (logLevel > LogLevel.Critical)
            {
                return;
            }
            File.AppendAllLines(logsFilePath, new[] { $@"[{DateTime.UtcNow}] [{type}] [CRITICAL] {message}" });
        }

        private FileLogger(string logsFilePath, Type type, int logLevel)
        {
            this.logsFilePath = Path.GetFullPath(logsFilePath);
            this.type = type;
            this.logLevel = logLevel;

            if (!Directory.Exists(Path.GetDirectoryName(logsFilePath)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(logsFilePath));
                File.Create(logsFilePath).Close();
            }

            var logsRetentionMaxTime = DateTime.Now.AddHours(-MAX_LOG_FILE_RETENTION_TIME_HOURS);
            bool shouldLogsBeCleared = (File.GetCreationTime(this.logsFilePath) <= logsRetentionMaxTime)
            || (new FileInfo(this.logsFilePath).Length / 1024 / 1024) > MAX_LOG_FILE_SIZE_MEGABYTES;

            if (shouldLogsBeCleared && File.Exists(this.logsFilePath))
            {
                File.Delete(logsFilePath);
                File.Create(logsFilePath).Close();
                File.SetCreationTime(logsFilePath, DateTime.Now);
            }
        }
    }
}