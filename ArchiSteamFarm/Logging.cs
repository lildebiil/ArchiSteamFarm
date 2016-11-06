﻿/*
    _                _      _  ____   _                           _____
   / \    _ __  ___ | |__  (_)/ ___| | |_  ___   __ _  _ __ ___  |  ___|__ _  _ __  _ __ ___
  / _ \  | '__|/ __|| '_ \ | |\___ \ | __|/ _ \ / _` || '_ ` _ \ | |_  / _` || '__|| '_ ` _ \
 / ___ \ | |  | (__ | | | || | ___) || |_|  __/| (_| || | | | | ||  _|| (_| || |   | | | | | |
/_/   \_\|_|   \___||_| |_||_||____/  \__|\___| \__,_||_| |_| |_||_|   \__,_||_|   |_| |_| |_|

 Copyright 2015-2016 Łukasz "JustArchi" Domeradzki
 Contact: JustArchi@JustArchi.net

 Licensed under the Apache License, Version 2.0 (the "License");
 you may not use this file except in compliance with the License.
 You may obtain a copy of the License at

 http://www.apache.org/licenses/LICENSE-2.0
					
 Unless required by applicable law or agreed to in writing, software
 distributed under the License is distributed on an "AS IS" BASIS,
 WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 See the License for the specific language governing permissions and
 limitations under the License.

*/

using System.Linq;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace ArchiSteamFarm {
	internal static class Logging {
		private const string LayoutMessage = @"${logger}|${message}${onexception:inner= ${exception:format=toString,Data}}";
		private const string GeneralLayout = @"${date:format=yyyy-MM-dd HH\:mm\:ss}|${processname}-${processid}|${level:uppercase=true}|" + LayoutMessage;
		private const string EventLogLayout = LayoutMessage;

		private static readonly ConcurrentHashSet<LoggingRule> ConsoleLoggingRules = new ConcurrentHashSet<LoggingRule>();

		private static bool IsWaitingForUserInput;

		internal static void InitLoggers() {
			if (LogManager.Configuration != null) {
				// User provided custom NLog config, or we have it set already, so don't override it
				InitConsoleLoggers();
				LogManager.ConfigurationChanged += OnConfigurationChanged;
				return;
			}

			LoggingConfiguration config = new LoggingConfiguration();

			ColoredConsoleTarget coloredConsoleTarget = new ColoredConsoleTarget("ColoredConsole") {
				DetectConsoleAvailable = false,
				Layout = GeneralLayout
			};

			config.AddTarget(coloredConsoleTarget);
			config.LoggingRules.Add(new LoggingRule("*", LogLevel.Trace, coloredConsoleTarget));

			if (Program.IsRunningAsService) {
				EventLogTarget eventLogTarget = new EventLogTarget("EventLog") {
					Layout = EventLogLayout,
					Log = SharedInfo.EventLog,
					Source = SharedInfo.EventLogSource
				};

				config.AddTarget(eventLogTarget);
				config.LoggingRules.Add(new LoggingRule("*", LogLevel.Trace, eventLogTarget));
			} else if (Program.Mode != Program.EMode.Client) {
				FileTarget fileTarget = new FileTarget("File") {
					DeleteOldFileOnStartup = true,
					FileName = SharedInfo.LogFile,
					Layout = GeneralLayout
				};

				config.AddTarget(fileTarget);
				config.LoggingRules.Add(new LoggingRule("*", LogLevel.Trace, fileTarget));
			}

			LogManager.Configuration = config;
			InitConsoleLoggers();
		}

		internal static void OnUserInputStart() {
			IsWaitingForUserInput = true;

			if (ConsoleLoggingRules.Count == 0) {
				return;
			}

			foreach (LoggingRule consoleLoggingRule in ConsoleLoggingRules) {
				LogManager.Configuration.LoggingRules.Remove(consoleLoggingRule);
			}

			LogManager.ReconfigExistingLoggers();
		}

		internal static void OnUserInputEnd() {
			IsWaitingForUserInput = false;

			if (ConsoleLoggingRules.Count == 0) {
				return;
			}

			foreach (LoggingRule consoleLoggingRule in ConsoleLoggingRules.Where(consoleLoggingRule => !LogManager.Configuration.LoggingRules.Contains(consoleLoggingRule))) {
				LogManager.Configuration.LoggingRules.Add(consoleLoggingRule);
			}

			LogManager.ReconfigExistingLoggers();
		}

		private static void InitConsoleLoggers() {
			ConsoleLoggingRules.Clear();
			foreach (LoggingRule loggingRule in LogManager.Configuration.LoggingRules.Where(loggingRule => loggingRule.Targets.Any(target => target is ColoredConsoleTarget || target is ConsoleTarget))) {
				ConsoleLoggingRules.Add(loggingRule);
			}
		}

		private static void OnConfigurationChanged(object sender, LoggingConfigurationChangedEventArgs e) {
			if ((sender == null) || (e == null)) {
				ASF.ArchiLogger.LogNullError(nameof(sender) + " || " + nameof(e));
				return;
			}

			InitConsoleLoggers();

			if (IsWaitingForUserInput) {
				OnUserInputStart();
			}
		}
	}
}
