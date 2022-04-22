using System.Collections.Concurrent;
using System.Management.Automation;
namespace TaskJob;

[Cmdlet(VerbsData.ConvertTo, "TaskJob")]
[OutputType(typeof(TaskJob))]
public sealed class ConvertToTaskJobCommand : PSCmdlet
{
    [Parameter(Mandatory = true, ValueFromPipeline = true)]
    [ValidateNotNullOrEmpty]
    public Task? Task;

    [Parameter()]
    [ValidateNotNullOrEmpty]
    public string? Name;

    protected override void ProcessRecord()
    {
        // Task should never be null due to mandatory parameter property
        if (Task is null)
        { throw new ArgumentNullException(nameof(Task)); }

        Name ??= "TaskJob" + Task.Id.ToString();
        TaskJob taskJob = new(Task, this, Name, null, null);
        WriteObject(taskJob);
    }
}

/// <summary>
/// ThreadJob
/// </summary>
public sealed class TaskJob : Job2
{
    readonly Task task;
    readonly Job2 managedJob;
    public TaskJob(Task Task, PSCmdlet Cmdlet, string? Name, string? Command, string? Location) : base(Command, Name)
    {
        PSJobTypeName = "TaskJob";
        this.Name = Name ?? "Task" + Task.Id;
        Output.EnumeratorNeverBlocks = true;
        task = Task;
        // Create a job definition and register it with the Powershell job manager so it can be seen via Get-Job.
        // The labels here don't seem to matter or show up anywhere
        JobDefinition jobDefinition = new(typeof(TaskJobSourceAdapter), "", "TaskJob");
        Dictionary<string, object> parameterCollection = new();
        parameterCollection.Add("NewJob", this);
        JobInvocationInfo jobSpecification = new(jobDefinition, parameterCollection);
        managedJob = Cmdlet.JobManager.NewJob(jobSpecification);
        task.Wait();
        dynamic result = ((dynamic)Task).Result;
        Output.Add(result);

        SetJobState(JobState.Completed);
    }

    public override bool HasMoreData => Output.Count > 0;

    public override string Location => "task";

    public override string StatusMessage => task.Status.ToString();

    public override void ResumeJob()
    {
        throw new NotImplementedException();
    }

    public override void ResumeJobAsync()
    {
        throw new NotImplementedException();
    }

    public override void StartJob()
    {
        // Tasks start automatically, we don't need to do it here
    }

    public override void StartJobAsync()
    {
        // Tasks start automatically, we don't need to do it here
    }

    public override void StopJob(bool force, string reason)
    {
        throw new NotImplementedException();
    }

    public override void StopJob()
    {
        throw new NotImplementedException();
    }

    public override void StopJobAsync()
    {
        throw new NotImplementedException();
    }

    public override void StopJobAsync(bool force, string reason)
    {
        throw new NotImplementedException();
    }

    public override void SuspendJob()
    {
        throw new NotImplementedException();
    }

    public override void SuspendJob(bool force, string reason)
    {
        throw new NotImplementedException();
    }

    public override void SuspendJobAsync()
    {
        throw new NotImplementedException();
    }

    public override void SuspendJobAsync(bool force, string reason)
    {
        throw new NotImplementedException();
    }

    public override void UnblockJob()
    {
        throw new NotImplementedException();
    }

    public override void UnblockJobAsync()
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// This is the catalog of thread jobs, instantiated once per processs and used by the job manager when Get-Job is called
/// </summary>
public sealed class TaskJobSourceAdapter : JobSourceAdapter
{
    private readonly ConcurrentDictionary<Guid, Job2> jobs = new();
    public override Job2 GetJobByInstanceId(Guid instanceId, bool recurse)
    {
        throw new NotImplementedException();
    }

    public override Job2 GetJobBySessionId(int id, bool recurse)
    {
        throw new NotImplementedException();
    }

    public override IList<Job2> GetJobsByCommand(string command, bool recurse)
    {
        throw new NotImplementedException();
    }

    public override IList<Job2> GetJobsByFilter(Dictionary<string, object> filter, bool recurse)
    {
        throw new NotImplementedException();
    }

    public override IList<Job2> GetJobsByName(string name, bool recurse)
    {
        throw new NotImplementedException();
    }

    public override IList<Job2> GetJobsByState(JobState state, bool recurse)
    {
        throw new NotImplementedException();
    }

    public override Job2 NewJob(JobInvocationInfo specification)
    {
        if (specification.Parameters[0][0].Value is not TaskJob job)
        {
            throw new InvalidDataException("No ThreadJob found in the JobSpecification parameters. This is probably a bug");
        }
        jobs.TryAdd(job.InstanceId, job);
        return job;
    }

    public override void RemoveJob(Job2 job)
    {
        throw new NotImplementedException();
    }

    public override IList<Job2> GetJobs()
    {
        return jobs.Values.ToList();
    }
}
