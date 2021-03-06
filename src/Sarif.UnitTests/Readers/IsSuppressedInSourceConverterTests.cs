﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Newtonsoft.Json;

namespace Microsoft.CodeAnalysis.Sarif.Readers
{
    [TestClass]
    public class InSourceSuppressionConverterTests : JsonTests
    {
        [TestMethod]
        public void SuppressionStatus_SuppressedInSource()
        {
            string expected =
@"{
  ""$schema"": """ + SarifSchemaUri + @""",
  ""version"": """ + SarifFormatVersion + @""",
  ""runs"": [
    {
      ""tool"": {
        ""name"": null
      },
      ""results"": [
        {
          ""suppressionStates"": [""suppressedInSource""]
        }
      ]
    }
  ]
}";
            string actual = GetJson(uut =>
            {
                var run = new Run();

                uut.Initialize(id: null, automationId: null);

                uut.WriteTool(DefaultTool);

                uut.WriteResults(new[] { new Result
                    {
                        SuppressionStates = SuppressionStates.SuppressedInSource
                    }
                });
            });
            Assert.AreEqual(expected, actual);

            var sarifLog = JsonConvert.DeserializeObject<SarifLog>(actual);
            Assert.AreEqual(SuppressionStates.SuppressedInSource, sarifLog.Runs[0].Results[0].SuppressionStates);
        }

        [TestMethod]
        public void BaselineState_None()
        {
            string expected =
@"{
  ""$schema"": """ + SarifSchemaUri + @""",
  ""version"": """ + SarifFormatVersion + @""",
  ""runs"": [
    {
      ""tool"": {
        ""name"": null
      },
      ""results"": [
        {}
      ]
    }
  ]
}";
            string actual = GetJson(uut =>
            {
                var run = new Run();
                uut.Initialize(id: null, automationId: null);
                uut.WriteTool(DefaultTool);

                uut.WriteResults(new[] { new Result
                    {
                        BaselineState = BaselineState.None
                    }
                });
            });
            Assert.AreEqual(expected, actual);

            var sarifLog = JsonConvert.DeserializeObject<SarifLog>(actual);
            Assert.AreEqual(SuppressionStates.None, sarifLog.Runs[0].Results[0].SuppressionStates);
            Assert.AreEqual(BaselineState.None, sarifLog.Runs[0].Results[0].BaselineState);
        }

        [TestMethod]
        public void BaselineState_Existing()
        {
            string expected =
@"{
  ""$schema"": """ + SarifSchemaUri + @""",
  ""version"": """ + SarifFormatVersion + @""",
  ""runs"": [
    {
      ""tool"": {
        ""name"": null
      },
      ""results"": [
        {
          ""baselineState"": ""existing""
        }
      ]
    }
  ]
}";
            string actual = GetJson(uut =>
            {
                var run = new Run();

                uut.Initialize(id: null, automationId: null);

                uut.WriteTool(DefaultTool);

                uut.WriteResults(new[] { new Result
                    {
                        BaselineState = BaselineState.Existing
                    }
                });
            });
            Assert.AreEqual(expected, actual);

            var sarifLog = JsonConvert.DeserializeObject<SarifLog>(actual);
            Assert.AreEqual(SuppressionStates.None, sarifLog.Runs[0].Results[0].SuppressionStates);
            Assert.AreEqual(BaselineState.Existing, sarifLog.Runs[0].Results[0].BaselineState);
        }
    }
}
