//
//      Copyright (C) 2012 DataStax Inc.
//
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.
//
﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Cassandra
{
    public class Options
    {
        public static Options Default = new Options();

        public readonly string IP_PREFIX;

        public readonly string CASSANDRA_VERSION;

        public string SSH_HOST;
        public int SSH_PORT;
        public string SSH_USERNAME;
        public string SSH_PASSWORD;

        public Options()
        {
            var version = Cassandra.Properties.Settings.Default.CASSANDRA_VERSION;
            IP_PREFIX = Cassandra.Properties.Settings.Default.IP_PREFIX;
            if (string.IsNullOrEmpty(IP_PREFIX))
                IP_PREFIX = "127.0.0.";

            CASSANDRA_VERSION = "-v " + version;

            SSH_HOST = Cassandra.Properties.Settings.Default.SSH_HOST;
            SSH_PORT = Cassandra.Properties.Settings.Default.SSH_PORT;
            SSH_USERNAME = Cassandra.Properties.Settings.Default.SSH_USERNAME;
            SSH_PASSWORD = Cassandra.Properties.Settings.Default.SSH_PASSWORD;

            USE_COMPRESSION = Cassandra.Properties.Settings.Default.USE_COMPRESSION;
            USE_NOBUFFERING = Cassandra.Properties.Settings.Default.USE_NOBUFFERING;
            USE_LOGGER = Cassandra.Properties.Settings.Default.USE_LOGGER;
        }

        public bool USE_COMPRESSION;
        public bool USE_NOBUFFERING;
        public bool USE_LOGGER;
    }
}
