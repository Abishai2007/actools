﻿using System.IO;
using System.Runtime.CompilerServices;
using AcTools.Utils;
using NUnit.Framework;

namespace AcTools.Tests {
    [TestFixture]
    public class FileUtilsTest {
        [Test]
        public void GetRelativePath() {
            Assert.AreEqual(@"system32\a.exe",
                    FileUtils.GetRelativePath(@"C:\Windows\system32\a.exe", @"C:\Windows"));

            Assert.AreEqual(@"system32\a.exe",
                    FileUtils.GetRelativePath(@"C:\WiNDows\system32\a.exe", @"C:\Windows\"));

            Assert.AreEqual(@"system32\a.exe",
                    FileUtils.GetRelativePath(@"C:\Windows\system32\a.exe", @"C:/WinDOws"));

            Assert.AreEqual(@"C:\Windows\system32\a.exe",
                    FileUtils.GetRelativePath(@"C:\Windows\system32\a.exe", @"C:\Windows\s"));

            Assert.AreEqual(@"",
                    FileUtils.GetRelativePath(@"C:\Windows", @"C:\Windows"));
        }

        [Test]
        public void NormalizePath() {
            Assert.AreEqual(@"C:\Windows", FileUtils.NormalizePath(@"C:\Windows"));
            Assert.AreEqual(@"C:\Windows", FileUtils.NormalizePath(@"C:\Windows\"));
            Assert.AreEqual(@"C:\Windows", FileUtils.NormalizePath(@"C:\/Windows\\"));
            Assert.AreEqual(@"C:\Windows", FileUtils.NormalizePath(@"C:\/Windows\\system32\.."));
            Assert.AreEqual(@"C:\Windows", FileUtils.NormalizePath(@"C:\.\Windows\."));
            Assert.AreEqual(@"C:\Windows", FileUtils.NormalizePath(@".\C:\.\Windows\..\Windows"));
        }

        [Test]
        public void IsAffected() {
            Assert.IsTrue(FileUtils.IsAffected(@"C:\Windows", @"C:\Windows\system32\a.exe"));
            Assert.IsTrue(FileUtils.IsAffected(@"C:\Windows\", @"C:\WiNDows\system32\a.exe"));
            Assert.IsTrue(FileUtils.IsAffected(@"C:/WinDOws", @"C:\Windows\system32\a.exe"));
            Assert.IsFalse(FileUtils.IsAffected(@"C:\Windows\s", @"C:\Windows\system32\a.exe"));
            Assert.IsTrue(FileUtils.IsAffected(@"C:\Windows", @"C:\Windows"));
            Assert.IsTrue(FileUtils.IsAffected(@"C:\Windows", @"c:/windows/system32"));
            Assert.IsFalse(FileUtils.IsAffected(@"C:\Windows", @"c:/wind"));
            Assert.IsFalse(FileUtils.IsAffected(@"C:\Windows2", @"c:/windows/system32"));
            Assert.IsFalse(FileUtils.IsAffected(@"C:\Windows\qwerty", @"c:/windows/system32/abc"));
            Assert.IsFalse(FileUtils.IsAffected(@"williams_fw24\content\driver\driver_fw24.kn5",
                    @"williams_fw24\content\cars\msf_williams_fw24\CAR_SUSP_LF.ksanim"));
        }

        private static string GetTestDir([CallerFilePath] string callerFilePath = null) => Path.Combine(Path.GetDirectoryName(callerFilePath) ?? "", "test");

        private static string TestDir => GetTestDir();

        [Test]
        public void EnsureUniqueTest() {
            var a = Path.Combine(TestDir, "a");
            Assert.AreEqual(a, FileUtils.EnsureUnique(a));

            var b = Path.Combine(TestDir, "b");
            Assert.AreEqual(Path.Combine(TestDir, "b-1"), FileUtils.EnsureUnique(b));

            var bt = Path.Combine(TestDir, "b.txt");
            Assert.AreEqual(Path.Combine(TestDir, "b-1.txt"), FileUtils.EnsureUnique(bt));

            var c = Path.Combine(TestDir, "c");
            Assert.AreEqual(Path.Combine(TestDir, "c-3"), FileUtils.EnsureUnique(c));

            var ct = Path.Combine(TestDir, "c.txt");
            Assert.AreEqual(Path.Combine(TestDir, "c-2.txt"), FileUtils.EnsureUnique(ct));
        }
    }
}