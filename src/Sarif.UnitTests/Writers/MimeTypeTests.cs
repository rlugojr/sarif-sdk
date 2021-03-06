﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CodeAnalysis.Sarif.Writers
{
    [TestClass]
    public class MimeTypeTests
    {
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void MimeType_GuessesFromFileName_DisallowsNullStringParameter()
        {
            MimeType.DetermineFromFileExtension((string)null);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void MimeType_GuessesFromFileName_DisallowsNullUriParameter()
        {
            MimeType.DetermineFromFileExtension((Uri)null);
        }

        [TestMethod]
        public void MimeType_GuessesFromFileName_Xml()
        {
            Assert.AreEqual("text/xml", MimeType.DetermineFromFileExtension("example.xml"));
        }

        [TestMethod]
        public void MimeType_GuessesFromFileName_Other()
        {
            Assert.AreEqual("application/octet-stream", MimeType.DetermineFromFileExtension("example.exe"));
        }

        [TestMethod]
        public void MimeType_GuessesFromFileName_IgnoresCase()
        {
            Assert.AreEqual("text/xml", MimeType.DetermineFromFileExtension("example.XmL"));
        }

        [TestMethod]
        public void MimeType_GuessesFromFileName_RequiresPeriod()
        {
            Assert.AreEqual("application/octet-stream", MimeType.DetermineFromFileExtension("examplexml"));
        }

        [TestMethod]
        public void MimeType_GuessesFromFileName_DealsWithTooShort()
        {
            Assert.AreEqual("text/xml", MimeType.DetermineFromFileExtension(".xml"));
        }

        [TestMethod]
        public void MimeType_Directory()
        {
            string directory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            Assert.AreEqual("application/x-directory", MimeType.DetermineFromFileExtension(directory));
        }

        [TestMethod]
        public void MimeType_DirectoryUri()
        {
            string directory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            Assert.AreEqual("application/x-directory", MimeType.DetermineFromFileExtension(new Uri(directory)));
        }
    }
}
