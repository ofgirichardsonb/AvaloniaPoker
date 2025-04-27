using System;
using System.Threading;
using System.Threading.Tasks;
using PokerGame.Foundation.Threading;
using Xunit;
using FluentAssertions;

namespace PokerGame.Tests.Foundation.Threading
{
    public class ThreadManagerTests
    {
        [Fact]
        public void StartThread_ShouldCreateAndStartNewThread()
        {
            // Arrange
            var threadManager = new ThreadManager();
            var threadStarted = new ManualResetEventSlim(false);
            string threadName = "TestThread";
            
            // Act
            string threadId = threadManager.StartThread(threadName, () => {
                threadStarted.Set();
                // Keep thread running for a bit
                Thread.Sleep(100);
            });
            
            // Wait for thread to start
            bool isStarted = threadStarted.Wait(1000);
            
            // Assert
            threadId.Should().NotBeNullOrEmpty();
            isStarted.Should().BeTrue("Thread should have started and signaled");
            threadManager.IsThreadRunning(threadId).Should().BeTrue("Thread should be reported as running");
            
            // Cleanup
            threadManager.StopThread(threadId);
            threadManager.StopAllThreads();
        }
        
        [Fact]
        public void StopThread_ShouldStopSpecifiedThread()
        {
            // Arrange
            var threadManager = new ThreadManager();
            var threadFinished = new ManualResetEventSlim(false);
            var cancellationDetected = new ManualResetEventSlim(false);
            
            string threadId = threadManager.StartThread("StoppableThread", (cancellationToken) => {
                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        Thread.Sleep(50);
                    }
                    cancellationDetected.Set();
                }
                finally
                {
                    threadFinished.Set();
                }
            });
            
            // Act
            threadManager.StopThread(threadId);
            
            // Wait for thread to detect cancellation and finish
            bool didDetectCancellation = cancellationDetected.Wait(1000);
            bool didFinish = threadFinished.Wait(1000);
            
            // Assert
            didDetectCancellation.Should().BeTrue("Thread should have detected cancellation");
            didFinish.Should().BeTrue("Thread should have finished");
            threadManager.IsThreadRunning(threadId).Should().BeFalse("Thread should no longer be running");
        }
        
        [Fact]
        public void StopAllThreads_ShouldStopAllRunningThreads()
        {
            // Arrange
            var threadManager = new ThreadManager();
            var thread1Finished = new ManualResetEventSlim(false);
            var thread2Finished = new ManualResetEventSlim(false);
            
            string thread1Id = threadManager.StartThread("Thread1", (token) => {
                while (!token.IsCancellationRequested)
                {
                    Thread.Sleep(50);
                }
                thread1Finished.Set();
            });
            
            string thread2Id = threadManager.StartThread("Thread2", (token) => {
                while (!token.IsCancellationRequested)
                {
                    Thread.Sleep(50);
                }
                thread2Finished.Set();
            });
            
            // Act
            threadManager.StopAllThreads();
            
            // Wait for threads to finish
            bool thread1DidFinish = thread1Finished.Wait(1000);
            bool thread2DidFinish = thread2Finished.Wait(1000);
            
            // Assert
            thread1DidFinish.Should().BeTrue("Thread 1 should have finished");
            thread2DidFinish.Should().BeTrue("Thread 2 should have finished");
            threadManager.IsThreadRunning(thread1Id).Should().BeFalse("Thread 1 should no longer be running");
            threadManager.IsThreadRunning(thread2Id).Should().BeFalse("Thread 2 should no longer be running");
        }
        
        [Fact]
        public void GetThreadInfo_ShouldReturnCorrectThreadInfo()
        {
            // Arrange
            var threadManager = new ThreadManager();
            string threadName = "InfoTestThread";
            
            string threadId = threadManager.StartThread(threadName, () => {
                // Keep thread running for a bit
                Thread.Sleep(500);
            });
            
            // Act
            var threadInfo = threadManager.GetThreadInfo(threadId);
            
            // Assert
            threadInfo.Should().NotBeNull();
            threadInfo.Name.Should().Be(threadName);
            threadInfo.IsRunning.Should().BeTrue();
            threadInfo.Id.Should().Be(threadId);
            
            // Cleanup
            threadManager.StopThread(threadId);
        }
        
        [Fact]
        public void GetAllThreads_ShouldReturnInfoForAllThreads()
        {
            // Arrange
            var threadManager = new ThreadManager();
            string thread1Name = "Thread1";
            string thread2Name = "Thread2";
            
            string thread1Id = threadManager.StartThread(thread1Name, () => Thread.Sleep(500));
            string thread2Id = threadManager.StartThread(thread2Name, () => Thread.Sleep(500));
            
            // Act
            var allThreads = threadManager.GetAllThreads();
            
            // Assert
            allThreads.Should().NotBeNull();
            allThreads.Should().HaveCount(2, "Two threads should be running");
            allThreads.Should().Contain(t => t.Id == thread1Id && t.Name == thread1Name);
            allThreads.Should().Contain(t => t.Id == thread2Id && t.Name == thread2Name);
            
            // Cleanup
            threadManager.StopAllThreads();
        }
        
        [Fact]
        public void WaitForThread_ShouldWaitForThreadCompletion()
        {
            // Arrange
            var threadManager = new ThreadManager();
            var threadCompletionTime = DateTime.MinValue;
            
            string threadId = threadManager.StartThread("WaitTestThread", () => {
                Thread.Sleep(200); // Thread runs for 200ms
                threadCompletionTime = DateTime.UtcNow;
            });
            
            // Act
            DateTime waitStartTime = DateTime.UtcNow;
            bool waitResult = threadManager.WaitForThread(threadId, 1000); // Wait up to 1 second
            DateTime waitEndTime = DateTime.UtcNow;
            
            // Assert
            waitResult.Should().BeTrue("Thread should complete within wait timeout");
            threadCompletionTime.Should().NotBe(DateTime.MinValue, "Thread should have completed");
            waitEndTime.Should().BeAfter(threadCompletionTime, "Wait should return after thread completion");
            (waitEndTime - waitStartTime).TotalMilliseconds.Should().BeGreaterOrEqualTo(200, "Wait should have lasted at least as long as the thread execution");
            threadManager.IsThreadRunning(threadId).Should().BeFalse("Thread should no longer be running after completion");
        }
        
        [Fact]
        public void WaitForThread_WithTimeout_ShouldReturnFalseIfThreadDoesNotComplete()
        {
            // Arrange
            var threadManager = new ThreadManager();
            
            string threadId = threadManager.StartThread("LongRunningThread", () => {
                Thread.Sleep(1000); // Thread runs for 1 second
            });
            
            // Act
            bool waitResult = threadManager.WaitForThread(threadId, 100); // Only wait 100ms
            
            // Assert
            waitResult.Should().BeFalse("Wait should timeout before thread completes");
            threadManager.IsThreadRunning(threadId).Should().BeTrue("Thread should still be running after wait timeout");
            
            // Cleanup
            threadManager.StopThread(threadId);
        }
        
        [Fact]
        public void IsThreadRunning_WithNonExistentThreadId_ShouldReturnFalse()
        {
            // Arrange
            var threadManager = new ThreadManager();
            string nonExistentThreadId = "non-existent-id";
            
            // Act
            bool isRunning = threadManager.IsThreadRunning(nonExistentThreadId);
            
            // Assert
            isRunning.Should().BeFalse("Non-existent thread should not be reported as running");
        }
    }
}