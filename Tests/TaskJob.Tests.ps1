#requires -Modules @{ModuleName = 'Pester'; ModuleVersion = '5.4.1'}
using namespace System.Diagnostics.CodeAnalysis
using namespace System.Net.Http
using namespace TaskJob

Describe 'TaskJob' {
  BeforeAll {
    Import-Module $PSScriptRoot/../obj/Debug/netstandard2.0/TaskJob.dll -Verbose -Force
  }
  Context 'ConvertTo-TaskJob' {
    BeforeAll {
      $uri = 'http://httpstat.us/200'
      $SCRIPT:task = [HttpClient]::new().GetStringAsync($uri)
    }
    It 'Converts a .net http request to a taskjob' {
      $task | ConvertTo-TaskJob | Should -BeOfType [TaskJob.TaskJob]
    }
    It 'Returns the proper result' {
      $task | ConvertTo-TaskJob | Wait-Job | Receive-Job | Should -Be '200 OK'
    }
  }
}