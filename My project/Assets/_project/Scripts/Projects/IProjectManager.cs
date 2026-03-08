using System;
using System.Collections.Generic;

namespace Maptifier.Projects
{
    public interface IProjectManager
    {
        string CurrentProjectId { get; }
        string CurrentProjectName { get; }
        bool HasUnsavedChanges { get; }

        void SaveProject(string name, Action<bool> onComplete = null);
        void LoadProject(string projectId, Action<bool> onComplete = null);
        void DeleteProject(string projectId);
        List<ProjectMetadata> ListProjects();
        ProjectData CreateNewProject(string name);

        void EnableAutoSave(float intervalSeconds = 60f);
        void DisableAutoSave();
    }

    [Serializable]
    public class ProjectMetadata
    {
        public string Id;
        public string Name;
        public string Created;
        public string Modified;
        public string ThumbnailPath;
    }
}
