﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EventStore.Common.CommandLine.lib;

namespace EventStore.Common.CommandLine
{
    public abstract class EventStoreCmdLineOptionsBase : CommandLineOptionsBase
    {
        public virtual IEnumerable<KeyValuePair<string, string>> GetLoadedOptionsPairs()
        {
            yield return new KeyValuePair<string, string>("LOGSPATH", LogsPath);
        }

        [Option(null, "logspath", HelpText = "Path where to keep log files")]
        public string LogsPath { get; set; }

        [HelpOption]
        public virtual string GetUsage()
        {
            return HelpText.AutoBuild(this, (HelpText current) => HelpText.DefaultParsingErrorsHandler(this, current));
        }

    }
}
