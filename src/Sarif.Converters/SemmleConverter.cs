﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualBasic.FileIO;

namespace Microsoft.CodeAnalysis.Sarif.Converters
{
    /// <summary>
    /// Converts a log file from the Semmle format to the SARIF format.
    /// </summary>
    internal class SemmleConverter : ToolFileConverterBase
    {
        // Semmle logs are CSV files.
        private static readonly string[] s_delimiters = new[] { "," };

        // The fields are as follows:
        private enum FieldIndex
        {
            QueryName,
            QueryDescription,
            Severity,
            Message,
            RelativePath,
            Path,
            StartLine,
            StartColumn,
            EndLine,
            EndColumn
        }

        private TextFieldParser _parser;
        private List<Notification> _toolNotifications;

        /// <summary>
        /// Converts a Semmle log file in CSV format to a SARIF log file.
        /// </summary>
        /// <param name="input">
        /// Input stream from which to read the Semmle log.
        /// </param>
        /// <param name="output">
        /// Output string to which to write the SARIF log.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when one or more required arguments are null.
        /// </exception>
        public override void Convert(Stream input, IResultLogWriter output)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            _toolNotifications = new List<Notification>();

            var results = GetResultsFromStream(input);

            var tool = new Tool
            {
                Name = "Semmle QL"
            };

            output.Initialize(id: null, automationId: null);

            output.WriteTool(tool);

            output.OpenResults();
            output.WriteResults(results);
            output.CloseResults();

            if (_toolNotifications.Any())
            {
                output.WriteToolNotifications(_toolNotifications);
            }
        }

        private Result[] GetResultsFromStream(Stream input)
        {
            var results = new List<Result>();
            using (_parser = new TextFieldParser(input)
            {
                TextFieldType = FieldType.Delimited,
                Delimiters = s_delimiters
            })
            {
                while (!_parser.EndOfData)
                {
                    results.Add(ParseResult());
                }
            }

            return results.ToArray();
        }

        private Result ParseResult()
        {
            string[] fields = _parser.ReadFields();

            string rawMessage = fields[(int)FieldIndex.Message];
            string normalizedMessage;

            IList<AnnotatedCodeLocation> relatedLocations = NormalizeRawMessage(rawMessage, out normalizedMessage);

            Region region = MakeRegion(fields);
            var result = new Result
            {
                Message = normalizedMessage,
                Locations = new Location[]
                {
                    new Location
                    {
                        ResultFile = new PhysicalLocation
                        {
                            Uri = new Uri(GetString(fields, FieldIndex.RelativePath), UriKind.Relative),
                            UriBaseId = "$srcroot",
                            Region = region
                        }
                    }
                },
                RelatedLocations = relatedLocations
            };

            ResultLevel level = ResultLevelFromSemmleSeverity(GetString(fields, FieldIndex.Severity));
            if (level != ResultLevel.Warning)
            {
                result.Level = level;
            }

            return result;
        }

        private IList<AnnotatedCodeLocation> NormalizeRawMessage(string rawMessage, out string normalizedMessage)
        {
            // The rawMessage contains embedded related locations. We need to extract the related locations and reformat the rawMessage without the embedded links.
            // Example rawMessage
            //     po (coming from [["hbm"|"relative://windows/Core/ntgdi/gre/brushapi.cxx:176:4882:3"],["hbm"|"relative://windows/Core/ntgdi/gre/windows/ntgdi.c:1873:50899:3"],["hbm"|"relative://windows/Core/ntgdi/gre/windows/ntgdi.c:5783:154466:3"]]) may not have been checked for validity before call to vSync.
            // Example normalizedMessage
            //     po (coming from "hbm") may not have been checked for validity before call to vSync.
            // Example relatedLocations
            //     relative://windows/Core/ntgdi/gre/brushapi.cxx:176:4882:3
            //     relative://windows/Core/ntgdi/gre/windows/ntgdi.c:1873:50899:3
            //     relative://windows/Core/ntgdi/gre/windows/ntgdi.c:5783:154466:3
            List<AnnotatedCodeLocation> relatedLocations = null;
            normalizedMessage = String.Empty;

            var sb = new StringBuilder();

            int index = rawMessage.IndexOf("[[");
            while (index > -1)
            {
                sb.Append(rawMessage.Substring(0, index));

                rawMessage = rawMessage.Substring(index + 2);

                index = rawMessage.IndexOf("]]");

                // embeddedLinksText contains the text for one set of embedded links except for the leading '[[' and trailing ']]'
                // "hbm"|"relative://windows/Core/ntgdi/gre/brushapi.cxx:176:4882:3"],["hbm"|"relative://windows/Core/ntgdi/gre/windows/ntgdi.c:1873:50899:3"],["hbm"|"relative://windows/Core/ntgdi/gre/windows/ntgdi.c:5783:154466:3"
                string embeddedLinksText = rawMessage.Substring(0, index - 1);

                // embeddedLinks splits the set of embedded links into invividual links
                // 1.  "hbm"|"relative://windows/Core/ntgdi/gre/brushapi.cxx:176:4882:3"
                // 2.  "hbm"|"relative://windows/Core/ntgdi/gre/windows/ntgdi.c:1873:50899:3"
                // 3.  "hbm"|"relative://windows/Core/ntgdi/gre/windows/ntgdi.c:5783:154466:3"

                string[] embeddedLinks = embeddedLinksText.Split(new string[] { "],[" }, StringSplitOptions.None);

                string tokenText = embeddedLinksText;
                foreach (string embeddedLink in embeddedLinks)
                {
                    string[] tokens = embeddedLink.Split(new char[] { '\"' }, StringSplitOptions.RemoveEmptyEntries);

                    // save the text portion of the link
                    tokenText = tokens[0];

                    string location = tokens[2];
                    tokens = location.Split(':');

                    relatedLocations = relatedLocations ?? new List<AnnotatedCodeLocation>();

                    var relatedLocation = new AnnotatedCodeLocation
                    {
                        PhysicalLocation = new PhysicalLocation
                        {
                            Uri = new Uri(tokens[1].Substring(1), UriKind.Relative),
                            UriBaseId = "$srcroot",
                            Region = new Region
                            {
                                StartLine = Int32.Parse(tokens[2]),
                                Offset = Int32.Parse(tokens[3]),
                                Length = Int32.Parse(tokens[4])
                            }
                        }
                    };
                    relatedLocations.Add(relatedLocation);
                }

                // re-add the text portion of the link
                sb.Append("\"" + tokenText + "\""); 

                rawMessage = rawMessage.Substring(index + "]]".Length);
                index = rawMessage.IndexOf("[[");
            }
            sb.Append(rawMessage);
            normalizedMessage = sb.ToString();
            return relatedLocations;
        }

        /// <summary>
        /// Create a Region object that contains only those properties required by the
        /// SARIF spec.
        /// </summary>
        /// <param name="fields">
        /// Array of fields from a CSV record.
        /// </param>
        /// <returns>
        /// A Region object that contains only those properties required by the SARIF spec.
        /// </returns>
        private Region MakeRegion(string[] fields)
        {
            Region region = new Region
            {
                StartLine = GetInteger(fields, FieldIndex.StartLine),
                StartColumn = GetInteger(fields, FieldIndex.StartColumn),
            };

            int endLine = GetInteger(fields, FieldIndex.EndLine);
            int endColumn = GetInteger(fields, FieldIndex.EndColumn);
            if (endLine != region.StartLine)
            {
                region.EndLine = endLine;
                region.EndColumn = endColumn;
            }
            else
            {
                if (endColumn != region.StartColumn)
                {
                    region.EndColumn = endColumn;
                }
            }

            return region;
        }

        private static string GetString(string[] fields, FieldIndex fieldIndex)
        {
            return fields[(int)fieldIndex];
        }

        private int GetInteger(string[] fields, FieldIndex fieldIndex)
        {
            string field = GetString(fields, fieldIndex);
            int value;
            if (!int.TryParse(field, out value))
            {
                value = 0;
                AddToolNotification(
                    "InvalidInteger",
                    NotificationLevel.Error,
                    ConverterResources.SemmleInvalidInteger,
                    field,
                    fieldIndex);
            }

            return value;
        }

        private ResultLevel ResultLevelFromSemmleSeverity(string semmleSeverity)
        {
            switch (semmleSeverity)
            {
                case "error":
                    return ResultLevel.Error;

                case "warning":
                    return ResultLevel.Warning;

                case "recommendation":
                    return ResultLevel.Note;

                default:
                    AddToolNotification(
                        "UnknownSeverity",
                        NotificationLevel.Error,
                        ConverterResources.SemmleUnknownSeverity,
                        semmleSeverity);
                    return ResultLevel.Warning;
            }
        }

        private void AddToolNotification(
            string id,
            NotificationLevel level,
            string messageFormat,
            params object[] args)
        {
            string message = string.Format(CultureInfo.CurrentCulture, messageFormat, args);

            // When the parser read the offending line, it incremented the line number,
            // so report the previous line.
            long lineNumber = _parser.LineNumber - 1;
            string messageWithLineNumber = string.Format(
                CultureInfo.CurrentCulture,
                ConverterResources.SemmleNotificationFormat,
                lineNumber,
                message);

            _toolNotifications.Add(new Notification
            {
                Id = id,
                Time = DateTime.Now,
                Level = level,
                Message = messageWithLineNumber
            });
        }
    }
}
