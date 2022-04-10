using NUnit.Framework;
using RollCall;
using System.IO;
using System;
using System.Collections.Generic;
using System.Linq;

namespace UTRollCall
{
    public class Tests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void CreateAndParseRollTextV01()
        {
            const string testScope = "Test";
            ulong seqno = 1;

            IRollWriter rollWriter = new RollWriterTextV01();
            var directory = new DirectoryInfo(".");
            var fileCollection = directory.EnumerateFiles("*", new EnumerationOptions { RecurseSubdirectories = false }).ToList();
            var rollFile = rollWriter.GenerateSequencedRoll(testScope, seqno, directory, fileCollection);
            Assert.IsNotNull(rollFile, "Failed to create the roll");

            IRollReader rollReader = new RollReaderText();
            var result = rollReader.TryReadRoll(rollFile, out var roll);
            Assert.IsTrue(result, "Failed to read the file");
            Assert.IsTrue(roll.Scope.Equals(testScope, StringComparison.Ordinal), "Scopes do not match");
            Assert.IsTrue(roll.Sequence == seqno, "Sequence number does not match");
            Assert.IsTrue(roll.Retransmit == 0, "Retransmit was meant to be 0");

            var expectedFiles = fileCollection.Where(fi => !fi.FullName.Equals(rollFile.FullName))
                                              .Select(fi => Path.GetRelativePath(directory.FullName, fi.FullName))
                                              .ToList();
            var rollFiles = roll.Files.Select(fe => fe.RelativePath).ToList();
            Assert.IsTrue(rollFiles.SequenceEqual(expectedFiles), "File entries do not match");
        }

    }
}