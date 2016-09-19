﻿using System;
using System.Collections.Generic;
using System.Linq;
using DynamicData.Kernel;
using FluentAssertions;
using Microsoft.Reactive.Testing;
using TailBlazer.Domain.FileHandling;
using Xunit;

namespace TailBlazer.Fixtures
{
   public class AutoTailFixture
    {
        [Fact]
        public void TailsLatestValuesOnly()
        {
            var scheduler = new TestScheduler();

            using (var file = new TestFile())
            {
                file.Append(CreateLines(1, 100));

                var autoTailer = new AutoTail(file.Info.WatchFile(scheduler: scheduler).Index());

                Line[] result = null;
                int counter = 0;
                using (autoTailer.Tail(10).Subscribe(x=> { result = x.AsArray(); counter++; }))
                {
                    //check that intial values are loaded
                    scheduler.AdvanceBySeconds(1);
                    counter.Should().Be(1);
                    result.Count().Should().Be(10);
                    var expected = CreateLines(91, 10);
                    result.Join().ShouldBeEquivalentTo(expected.Join());

                    file.Append(CreateLines(101, 5));
                    scheduler.AdvanceBySeconds(1);
                    counter.Should().Be(2);
                    result.Count().Should().Be(5);
                    expected = CreateLines(101, 5);
                    result.Join().ShouldBeEquivalentTo(expected.Join());
                }
            }
        }

        [Fact]
        public void TailsLatestValuesOnly_ForFilteredValues()
        {
            var scheduler = new TestScheduler();

            using (var file = new TestFile("TailsLatestValuesOnly_ForFilteredValues"))
            {
                file.Append(CreateLines(1, 100));

                var autoTailer = new AutoTail(file.Info.WatchFile(scheduler: scheduler).Search(str => str.Contains("9")));

                Line[] result = null;
                int counter = 0;
                using (autoTailer.Tail(10).Subscribe(x => { result = x.AsArray(); counter++; }))
                {
                    //check that intial values are loaded
                    scheduler.AdvanceBySeconds(1);
                     counter.Should().Be(1);
                    result.Count().Should().Be(10);
                    var expected = CreateLines(90, 10);
                    result.Join().ShouldBeEquivalentTo(expected.Join());

                    file.Append(CreateLines(101, 10));
                    scheduler.AdvanceBySeconds(1);
                    counter.Should().Be(2);
                    result.Count().Should().Be(1);
                    expected = CreateLines(109, 1);
                    result.Join().ShouldBeEquivalentTo(expected.Join());
                }
            }
        }


        private string[] CreateLines(int start, int take)
        {
            return Enumerable.Range(start, take).Select(i => $"This is line number {i.ToString("00000000")}").ToArray();
        }
    }

    internal static class LinesEx
    {
        public static string Join(this IEnumerable<string> lines)
        {
            return string.Join(Environment.NewLine, lines);
        }

        public static string Join(this IEnumerable<Line> lines)
        {
            return string.Join(Environment.NewLine, lines.Select(l=>l.Text));
        }
    }
}