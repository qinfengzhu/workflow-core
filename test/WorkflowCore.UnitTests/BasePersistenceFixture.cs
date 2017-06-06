﻿using System;
using System.Collections.Generic;
using System.Text;
using WorkflowCore.Interface;
using WorkflowCore.Models;
using Xunit;
using FluentAssertions;
using WorkflowCore.TestAssets;
using System.Threading.Tasks;

namespace WorkflowCore.UnitTests
{
    public abstract class BasePersistenceFixture
    {
        protected abstract IPersistenceProvider Subject { get; }

        [Fact]
        public void CreateNewWorkflow()
        {
            var workflow = new WorkflowInstance()
            {
                Data = new { Value1 = 7 },
                Description = "My Description",
                Status = WorkflowStatus.Runnable,
                NextExecution = 0,
                Version = 1,
                WorkflowDefinitionId = "My Workflow"
            };
            workflow.ExecutionPointers.Add(new ExecutionPointer()
            {
                Id = Guid.NewGuid().ToString(),
                Active = true,
                StepId = 0
            });

            var workflowId = Subject.CreateNewWorkflow(workflow).Result;

            workflowId.Should().NotBeNull();
            workflow.Id.Should().NotBeNull();
        }

        [Fact]
        public void GetWorkflowInstance()
        {
            var workflow = new WorkflowInstance()
            {
                Data = new { Value1 = 7 },
                Description = "My Description",
                Status = WorkflowStatus.Runnable,
                NextExecution = 0,
                Version = 1,
                WorkflowDefinitionId = "My Workflow"
            };
            workflow.ExecutionPointers.Add(new ExecutionPointer()
            {
                Id = Guid.NewGuid().ToString(),
                Active = true,
                StepId = 0
            });
            var workflowId = Subject.CreateNewWorkflow(workflow).Result;

            var retrievedWorkflow = Subject.GetWorkflowInstance(workflowId).Result;

            retrievedWorkflow.ShouldBeEquivalentTo(workflow);
        }

        [Fact]
        public void PersistWorkflow()
        {
            var oldWorkflow = new WorkflowInstance()
            {
                Data = new { Value1 = 7 },
                Description = "My Description",
                Status = WorkflowStatus.Runnable,
                NextExecution = 0,
                Version = 1,
                WorkflowDefinitionId = "My Workflow",
                CreateTime = new DateTime(2000, 1, 1).ToUniversalTime()
            };
            oldWorkflow.ExecutionPointers.Add(new ExecutionPointer()
            {
                Id = Guid.NewGuid().ToString(),
                Active = true,
                StepId = 0
            });
            var workflowId = Subject.CreateNewWorkflow(oldWorkflow).Result;
            var newWorkflow = Utils.DeepCopy(oldWorkflow);
            newWorkflow.NextExecution = 7;
            newWorkflow.ExecutionPointers.Add(new ExecutionPointer() { Id = Guid.NewGuid().ToString(), Active = true, StepId = 1 });

            Subject.PersistWorkflow(newWorkflow).Wait();

            var current = Subject.GetWorkflowInstance(workflowId).Result;
            current.ShouldBeEquivalentTo(newWorkflow);
        }

        [Fact]
        public void ConcurrentPersistWorkflow()
        {
            var subject = Subject; // Don't initialize in the thread.

            var tasks = new List<Task<string>>();

            for (int i = 0; i < 30; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    try
                    {
                        var oldWorkflow = new WorkflowInstance()
                        {
                            Data = new { Value1 = 7 },
                            Description = "My Description",
                            Status = WorkflowStatus.Runnable,
                            NextExecution = 0,
                            Version = 1,
                            WorkflowDefinitionId = "My Workflow",
                            CreateTime = new DateTime(2000, 1, 1).ToUniversalTime()
                        };
                        oldWorkflow.ExecutionPointers.Add(new ExecutionPointer()
                        {
                            Id = Guid.NewGuid().ToString(),
                            Active = true,
                            StepId = 0
                        });
                        var workflowId = subject.CreateNewWorkflow(oldWorkflow).Result;
                        var newWorkflow = Utils.DeepCopy(oldWorkflow);
                        newWorkflow.NextExecution = 7;
                        newWorkflow.ExecutionPointers.Add(new ExecutionPointer() { Id = Guid.NewGuid().ToString(), Active = true, StepId = 1 });

                        subject.PersistWorkflow(newWorkflow).Wait(); // It will throw an exception if the persistence provider occurred resource competition.

                        return workflowId;
                    }
                    catch (Exception)
                    {
                        return null;
                    }
                }));
            }

            Task.WhenAll(tasks).Wait();

            foreach (var task in tasks)
            {
                task.Result.Should().NotBeNull();
            }
        }
    }
}
