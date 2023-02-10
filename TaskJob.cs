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

	[Parameter()]
	public string? Command;

	[Parameter()]
	public string? Location;

	protected override void ProcessRecord()
    {
        // Task should never be null due to mandatory parameter property
        if (Task is null)
        { throw new ArgumentNullException(nameof(Task)); }

        Name ??= "TaskJob" + Task.Id.ToString();
		TaskJob taskJob = new(Task, this, Name, Command, Location);
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
	readonly string location;
	public TaskJob(Task Task, PSCmdlet Cmdlet, string? Name, string? Command, string? Location) : base(Command, Name)
    {
        PSJobTypeName = "TaskJob";
        this.Name = Name ?? "Task" + Task.Id;
		task = Task;
		Location ??= "task";
		location = Location;

		Output.EnumeratorNeverBlocks = true;

		// Create a job definition and register it with the PowerShell job manager so it can be seen via Get-Job.
		// The labels here don't seem to matter or show up anywhere
		JobDefinition jobDefinition = new(typeof(TaskJobSourceAdapter), Command, Name);
        Dictionary<string, object> parameterCollection = new()
        {
            { "NewJob", this }
        };
        JobInvocationInfo jobSpecification = new(jobDefinition, parameterCollection);
		managedJob = Cmdlet.JobManager.NewJob(jobSpecification);
		task.Wait();
        dynamic result = ((dynamic)Task).Result;
		Output.Add(result);
		SetJobState(JobState.Completed);
	}

    public override bool HasMoreData => Output.Count > 0;

	public override string Location => location;

	public override string StatusMessage => task.Status.ToString();



	public override void ResumeJobAsync()
	{
		throw new NotSupportedException("Tasks do not natively support being suspended or resumed");
	}
	public override void ResumeJob() { ResumeJob(); }

	// Tasks start automatically, we don't need to do it here
	public override void StartJob() { }

	public override void StartJobAsync() { }

    public override void StopJob(bool force, string reason)
    {
		throw new NotSupportedException("Tasks cannot be cancelled directly, you must run Cancel() on a tokenSource");
    }

	public override void StopJob() { StopJob(true, string.Empty); }

	public override void StopJobAsync() { StopJob(); }

	public override void StopJobAsync(bool force, string reason) { StopJob(force, reason); }

	public override void SuspendJob(bool force, string reason)
	{
		throw new NotSupportedException("Tasks do not natively support being suspended or resumed");
	}

	public override void SuspendJob() { SuspendJobAsync(); }

	public override void SuspendJobAsync() { SuspendJob(true, string.Empty); }

	public override void SuspendJobAsync(bool force, string reason) { SuspendJob(force, reason); }

    public override void UnblockJob()
    {
		throw new NotSupportedException("Tasks do not natively support being blocked or unblocked");
    }

	public override void UnblockJobAsync() { UnblockJob(); }
}

/// <summary>
/// This is the catalog of thread jobs, instantiated once per processs and used by the job manager when Get-Job is called
/// </summary>
public sealed class TaskJobSourceAdapter : JobSourceAdapter
{
    private readonly ConcurrentDictionary<Guid, Job2> jobs = new();
    public override Job2 GetJobByInstanceId(Guid instanceId, bool recurse)
    {
		return jobs[instanceId];
	}

    public override Job2 GetJobBySessionId(int id, bool recurse)
    {
		return jobs.Values.Where(job => job.Id.Equals(id)).Single();
	}

    public override IList<Job2> GetJobsByCommand(string command, bool recurse)
    {
		return jobs.Values.Where(job => job.Command.Equals(command)).ToList();
	}

    public override IList<Job2> GetJobsByFilter(Dictionary<string, object> filter, bool recurse)
    {
		throw new NotImplementedException("The TaskJobSourceAdapter has no adapter-specific filters");
    }

    public override IList<Job2> GetJobsByName(string name, bool recurse)
    {
		return jobs.Values.Where(job => job.Name.Equals(name)).ToList();
    }

    public override IList<Job2> GetJobsByState(JobState state, bool recurse)
    {
		return jobs.Values.Where(job => job.JobStateInfo.State.Equals(state)).ToList();
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
		if (!jobs.TryRemove(job.InstanceId, out _))
		{
			throw new InvalidOperationException($"Job {job.Name} ({job.InstanceId}) does not exist in the catalog");
		}
	}

    public override IList<Job2> GetJobs()
    {
        return jobs.Values.ToList();
    }
}
