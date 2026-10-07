using Focus_Bear.Utils;
using FocusBear.Core.ToDo;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Focus_Bear.ToDoScreens
{
    /// <summary>
    /// User-facing strings for the native To-Do screen, from the shared config's <c>toDoListViewInfo</c>
    /// section — the same keys the Mac's <c>ToDoStrings.swift</c> reads — with its English fallbacks.
    /// Templates use the Mac's <c>&lt;TITLE&gt;</c>-style tokens.
    /// </summary>
    public sealed class ToDoStrings
    {
        public const string TitleToken = "<TITLE>";
        public const string CountToken = "<COUNT>";
        public const string DateToken = "<DATE>";
        public const string ScoreToken = "<SCORE>";
        public const string ProjectToken = "<PROJECT>";
        public const string PhraseToken = "<PHRASE>";
        private const char ListSeparator = '|';

        private readonly JObject _info;
        private readonly JObject _forecast;

        private ToDoStrings(JObject info, JObject forecast = null)
        {
            _info = info;
            _forecast = forecast;
        }

        /// <summary>The English fallbacks alone, with no config read — for tests.</summary>
        internal static ToDoStrings Fallbacks() => new ToDoStrings(null);

        public static ToDoStrings Load()
        {
            var config = AppSettings.GetInstance.ConfigFileStrings;
            return new ToDoStrings(config != null ? config["toDoListViewInfo"] as JObject : null,
                                   config != null ? config["localForecastViewInfo"] as JObject : null);
        }

        private string S(string key, string fallback)
        {
            var value = _info != null ? _info[key] as JValue : null;
            string text = value != null && value.Type == JTokenType.String ? (string)value : null;
            return string.IsNullOrEmpty(text) ? fallback : text;
        }

        public static string Format(string template, string token, string value) => (template ?? "").Replace(token, value ?? "");

        // Shell
        public string Title => AppSettings.GetInstance.ConfigFileStrings?["leftMenuVcInfo"]?["menuTodoList"]?.ToString() ?? "To-Do List";
        public string Cancel => S("cancelText", "Cancel");
        public string Save => S("saveText", "Save");
        public string Saving => S("savingText", "Saving…");
        public string Delete => S("deleteText", "Delete");
        public string Discard => S("discardText", "Discard");
        public string Loading => S("loadingText", "Loading…");
        public string RequestFailed => S("requestFailedText", "Could not process the request");

        // Toolbar
        public string NewTask => S("newTaskText", "New Task");
        public string SearchPlaceholder => S("searchPlaceholder", "Search...");
        public string Filter => S("filterText", "Filter");
        public string MoreOptions => S("moreOptionsText", "More Options");
        public string SortSmart => S("sortSmartText", "Smart");
        public string SortSmartTooltip => S("sortSmartTooltip", "Smart sort — ADHD-friendly. Sorted by TOP score, highest priority first.");
        public string SortChronological => S("sortChronologicalText", "Newest");
        public string SortChronologicalTooltip => S("sortChronologicalTooltip", "Chronological sort. Most recently created tasks first.");
        public string SortDueDate => S("sortDueDateText", "Due date");
        public string SortDueDateTooltip => S("sortDueDateTooltip", "Due date sort. Overdue first, then soonest due. Tasks without a due date last.");
        public string ExportCsv => S("exportCsvText", "Export CSV");
        public string ExportCsvTooltip => S("exportCsvTooltip", "Exports all tasks. Filtered views are not applied to the export.");
        public string ExportSaved => S("exportSavedText", "Tasks exported");
        public string ExportFailed => S("exportFailedText", "Couldn't load all tasks — exported the tasks already loaded.");
        public string EisenhowerMatrix => S("eisenhowerMatrixText", "View Eisenhower Matrix");
        public string ManageIntegrations => S("manageIntegrationsText", "Manage integrations on the web");
        public string Refresh => S("refreshText", "Refresh");

        public string SortTitle(ToDoSortMode mode)
        {
            switch (mode)
            {
                case ToDoSortMode.Chronological: return SortChronological;
                case ToDoSortMode.DueDate: return SortDueDate;
                default: return SortSmart;
            }
        }

        public string SortTooltip(ToDoSortMode mode)
        {
            switch (mode)
            {
                case ToDoSortMode.Chronological: return SortChronologicalTooltip;
                case ToDoSortMode.DueDate: return SortDueDateTooltip;
                default: return SortSmartTooltip;
            }
        }

        // Filters
        public string FilterDueDate => S("filterDueDateText", "Due Date");
        public string FilterEnergyLevel => S("filterEnergyLevelText", "Energy Level");
        public string FilterProject => S("filterProjectText", "Project");
        public string FilterAll => S("filterAllText", "All");
        public string LowSpoons => S("lowSpoonsText", "Low spoons");
        public string LowSpoonsSubtitle => S("lowSpoonsSubtitle", "Perspiration level 0 - 3");
        public string FullSpoons => S("fullSpoonsText", "Full spoons");
        public string FullSpoonsSubtitle => S("fullSpoonsSubtitle", "Perspiration level 4 - 10");
        public string DueToday => S("dueTodayText", "Due Today");
        public string DueThisWeek => S("dueThisWeekText", "Due This Week");
        public string DueNextWeek => S("dueNextWeekText", "Due Next Week");
        public string Next30Days => S("next30DaysText", "Next 30 Days");
        public string ClearFilters => S("clearFiltersText", "Clear filters");

        // Sections
        public string SectionDoing => S("sectionDoingText", "Doing");
        public string SectionToDo => S("sectionToDoText", "To do");
        public string SectionDone => S("sectionDoneText", "Done");
        public string SectionEmpty => S("sectionEmptyText", "No tasks in this section.");
        public string AddYourFirstTask => S("addYourFirstTaskText", "🚀 Add your first task");
        public string NoTasksMatchFilters => S("noTasksMatchFiltersText", "No tasks match your filters");
        public string CouldNotFindTasks => S("couldNotFindTasksText", "Could not find any tasks");
        public string LoadMore => S("loadMoreText", "Load more");

        public IDictionary<ToDoStatus, string> SectionTitles() => new Dictionary<ToDoStatus, string>
        {
            { ToDoStatus.InProgress, SectionDoing },
            { ToDoStatus.NotStarted, SectionToDo },
            { ToDoStatus.Completed, SectionDone },
        };

        // Row
        public string MarkComplete => S("markCompleteText", "Mark \"<TITLE>\" complete");
        public string MarkCompleteShort => S("markCompleteShortText", "Mark complete");
        public string MarkIncomplete => S("markIncompleteText", "Mark incomplete");
        public string Snooze => S("snoozeText", "Snooze");
        public string Edit => S("editText", "Edit");
        public string Subtasks => S("subtasksText", "Subtasks");
        public string DueShort => S("dueShortText", "Due");
        public string CreatedShort => S("createdShortText", "Created");
        public string TopShort => S("topShortText", "TOP");
        public string Created => S("createdText", "Created <DATE>");
        public string Today => S("todayText", "Today");
        public string Tomorrow => S("tomorrowText", "Tomorrow");
        public string Yesterday => S("yesterdayText", "Yesterday");
        public string NoDueDate => S("noDueDateText", "No due date");
        public string TopScoreTooltip => S("topScoreTooltip", "TOP ranks which task to do next. Higher means more urgent and valuable relative to effort.");

        // Due-date popover
        public string EditDueDate => S("editDueDateText", "Edit due date");
        public string DueDatePlaceholder => S("dueDatePlaceholder", "e.g. 2d, 30th, fri");
        public string UnrecognisedDate => S("unrecognisedDateText", "Unrecognised date");
        public string DueDateGrammarHelp => S("dueDateGrammarHelp", "Type a date and press Enter: 2d, 3w, 1m (months), 2y (years), -1d for yesterday, fri, next fri, tomorrow, 28th, 28 sep, 25/9, 2026-09-25, in 2 days");
        public string NextWeek => S("nextWeekText", "Next week");
        public string Clear => S("clearText", "Clear");

        // Editor
        public string CreateTaskTitle => S("createTaskTitle", "New task");
        public string Selected => S("selectedText", "Selected");
        public string EditTaskTitle => S("editTaskTitle", "Edit task");
        public string AddATask => S("addATaskPlaceholder", "Add a task");
        public string TitleIsNeeded => S("titleIsNeededText", "Write a title to save the task");
        public string UnsavedChanges => S("unsavedChangesText", "Your changes will be lost. Discard them?");
        public string Status => S("statusText", "Status");
        public string StatusNotStarted => S("statusNotStartedText", "Not Started");
        public string StatusInProgress => S("statusInProgressText", "In Progress");
        public string StatusCompleted => S("statusCompletedText", "Completed");
        public string StatusAppliesTo => S("statusAppliesToText", "Status applies to");
        public string TopScore => S("topScoreText", "TOP score");
        public string TimelineDueDate => S("timelineDueDateText", "Timeline (due date)");
        public string Outcome => S("outcomeText", "Outcome");
        public string Perspiration => S("perspirationText", "Perspiration");
        public string Projects => S("projectsText", "Projects");
        public string SearchProjectsPlaceholder => S("searchProjectsPlaceholder", "Search projects...");
        public string NoExistingProjects => S("noExistingProjectsText", "No existing projects");
        public string NoProjectsFound => S("noProjectsFoundText", "No projects found");
        public string CreateProjectNamed => S("createProjectNamedText", "Create “<PROJECT>”");
        public string Description => S("descriptionText", "Description");
        public string CreateSubtasks => S("createSubtasksText", "Create subtasks");
        public string AddASubtask => S("addASubtaskPlaceholder", "Add a subtask…");
        public string GenerateSubtasksViaAI => S("generateSubtasksViaAIText", "Generate Subtasks via AI");
        public string AddSubtask => S("addSubtaskText", "Add Subtask");
        public string Quadrant => S("quadrantText", "Eisenhower quadrant");
        public string QuadrantDo => S("quadrantDoText", "Do");
        public string QuadrantDecide => S("quadrantDecideText", "Decide");
        public string QuadrantDelegate => S("quadrantDelegateText", "Delegate");
        public string QuadrantDelete => S("quadrantDeleteText", "Delete");
        public string QuadrantUrgentImportant => S("quadrantUrgentImportantText", "Urgent and Important");
        public string QuadrantImportantNotUrgent => S("quadrantImportantNotUrgentText", "Important but not urgent");
        public string QuadrantNotImportantUrgent => S("quadrantNotImportantUrgentText", "Not important and urgent");
        public string QuadrantNotImportantNotUrgent => S("quadrantNotImportantNotUrgentText", "Not important and not urgent");
        public string Objective => S("objectiveText", "Objective");
        public string ObjectivePlaceholder => S("objectivePlaceholder", "Why do you want to do this task?");
        public string Minutes => S("minutesText", "minutes");
        public string DeleteTaskConfirmation => S("deleteTaskConfirmationText", "Are you sure you want to delete this task?");
        public string DeleteTask => S("deleteTaskText", "Delete task");
        public string NotSet => "—";

        // Feedback
        public string TaskCreated => S("taskCreatedText", "Task created successfully");
        public string CouldNotCreateTask => S("couldNotCreateTaskText", "Could not create the task");
        public string TaskUpdated => S("taskUpdatedText", "Task updated successfully");
        public string CouldNotUpdateTask => S("couldNotUpdateTaskText", "Could not update the status of task with title: <TITLE>");
        public string TaskDeleted => S("taskDeletedText", "Task deleted successfully");
        public string CouldNotDeleteTask => S("couldNotDeleteTaskText", "Could not delete the task");
        public string TaskCompletedSingle => S("taskCompletedSingleText", "<PHRASE>, '<TITLE>' completed!");
        public string TaskCompletedMultiple => S("taskCompletedMultipleText", "<PHRASE>, you're on a roll! <COUNT> tasks completed");
        public string SubtasksGenerated => S("subtasksGeneratedText", "Subtasks generated successfully");
        public string CouldNotGenerateSubtasks => S("couldNotGenerateSubtasksText", "Could not generate subtasks");
        public string CouldNotCreateProject => S("couldNotCreateProjectText", "Could not create project");
        public string SyncedProject => S("syncedProjectText", "Synced project");

        // Tools
        public string BrainDumpToTasks => S("brainDumpToTasksText", "Use AI to convert brain dump to tasks");
        public string BrainDumpTitle => S("brainDumpTitle", "Brain dump");
        public string TasksFromBrainDump => S("tasksFromBrainDumpText", "Tasks from Brain Dump");
        public string BrainDumpPlaceholder => S("brainDumpPlaceholder", "Dump down ideas you have so you don't forget");
        public string VagueInputPrompt => S("vagueInputPromptText", "Your input seems a bit brief. Adding more details will help generate better tasks. Try describing what you want to accomplish in more detail.");
        public string ConvertAnyway => S("convertAnywayText", "Convert anyway");
        public string Converting => S("convertingText", "Converting…");
        public string CreateTasks => S("createTasksText", "Create tasks");
        public string TasksWillBeCreated => S("tasksWillBeCreatedText", "<COUNT> tasks will be created");
        public string BrainDumpFailed => S("brainDumpFailedText", "Sorry, we were unable to convert your brain dump. Please try again.");
        public string BrainDumpLimit => S("brainDumpLimitText", "Use up to <COUNT> characters for task suggestions. Your original note is unchanged.");
        public string BrainDumpPartialSave => S("brainDumpPartialSaveText", "Confirmed <COUNT> tasks saved. The remaining tasks are still here. Review them and try again.");
        public string BrainDumpInvalidDraft => S("brainDumpInvalidDraftText", "Give each task a title and a valid estimate greater than zero minutes, or remove it before saving.");
        public string Close => S("closeText", "Close");
        public string Add => S("addText", "Add");
        public string Urgent => S("urgentText", "Urgent");
        public string NotUrgent => S("notUrgentText", "Not urgent");
        public string Important => S("importantText", "Important");
        public string NotImportant => S("notImportantText", "Not important");
        public string Procrastinate => S("procrastinateText", "Procrastinate");
        public string ProcrastinateInfo => S("procrastinateInfoText", "Save distracting links for later so you can get back to your task now.");
        public string Url => S("urlText", "URL");
        public string Note => S("noteText", "Note");
        public string InvalidUrl => S("invalidUrlText", "Enter a valid URL");
        public string DuplicateUrl => S("duplicateUrlText", "This URL is already saved");
        public string DeleteAll => S("deleteAllText", "Delete All");
        public string NoUrlsFound => S("noUrlsFoundText", "No URLs found!");
        public string WebsitesDeleted => S("websitesDeletedText", "Websites deleted successfully");
        public string CouldNotDeleteWebsites => S("couldNotDeleteWebsitesText", "Couldn't delete saved websites");

        // Attachments
        public string AttachmentPending => S("attachmentPendingText", "Ready to upload when you save the task.");
        public string AttachmentRetry => S("attachmentRetryText", "The task was saved. Save again to retry the remaining files.");
        public string AttachmentUnconfirmed => S("attachmentUnconfirmedText", "The upload result is not confirmed. Save again to check its status.");
        public string Attachments => S("attachmentsText", "Attachments");
        public string AddAttachment => S("addAttachmentText", "Add file…");
        public string NoAttachmentsYet => S("noAttachmentsYetText", "No attachments yet");
        public string AttachmentFailed => S("attachmentFailedText", "Could not upload the file. Please try again.");
        public string AttachmentTooLarge => S("attachmentTooLargeText", "Files must be 20 MB or smaller.");
        public string AttachmentLimitReached => S("attachmentLimitReachedText", "A task can hold 20 attachments.");
        public string AttachmentUnreadable => S("attachmentUnreadableText", "Could not read that file.");
        public string AttachmentEmpty => S("attachmentEmptyText", "That file is empty.");
        public string AttachmentsLoadFailed => S("attachmentsLoadFailedText", "Could not load the attachments.");
        public string AttachmentUploading => S("attachmentUploadingText", "Uploading…");
        public string AttachmentDeleteFailed => S("attachmentDeleteFailedText", "Could not delete the attachment.");
        public string DeleteAttachment => S("deleteAttachmentText", "Delete attachment");

        // Comments and reactions
        public string Comments => S("commentsText", "Comments");
        public string NoCommentsYet => S("noCommentsYetText", "No comments yet");
        public string AddCommentPlaceholder => S("addCommentPlaceholder", "Add a comment...");
        public string Reactions => S("reactionsText", "Reactions");
        public string CommentAdded => S("commentAddedText", "Comment added successfully");
        public string CouldNotAddComment => S("couldNotAddCommentText", "Could not add comment");
        public string CommentDeleted => S("commentDeletedText", "Comment deleted successfully");
        public string CouldNotDeleteComment => S("couldNotDeleteCommentText", "Could not delete comment");
        public string CommentUpdated => S("commentUpdatedText", "Comment updated successfully");
        public string CouldNotUpdateComment => S("couldNotUpdateCommentText", "Could not update comment");
        public string EditComment => S("editCommentText", "Edit comment");
        public string DeleteComment => S("deleteCommentText", "Delete comment");
        public string CouldNotAddReaction => S("couldNotAddReactionText", "Could not add reaction");
        public string CouldNotRemoveReaction => S("couldNotRemoveReactionText", "Could not remove reaction");

        // Projects
        public string TabTasks => S("tabTasksText", "Tasks");
        public string TabProjects => S("tabProjectsText", "Projects");
        public string ViewWhatMatters => S("viewWhatMattersText", "What matters");
        public string ViewAllProjects => S("viewAllProjectsText", "All projects");
        public string NewProject => S("newProjectText", "New Project");
        public string ProjectNamePlaceholder => S("projectNamePlaceholder", "Project name");
        public string ProjectSearchPlaceholder => S("projectSearchPlaceholder", "Search projects…");
        public string ProjectStatusActive => S("projectStatusActiveText", "Active");
        public string ProjectStatusCompleted => S("projectStatusCompletedText", "Completed");
        public string ProjectStatusArchived => S("projectStatusArchivedText", "Archived");
        public string NoProjectsYet => S("noProjectsYetText", "No projects yet");
        public string CreateFirstProject => S("createFirstProjectText", "Create your first project to get started");
        public string NoMatchingProjects => S("noMatchingProjectsText", "No matching projects");
        public string TryDifferentSearch => S("tryDifferentSearchText", "Try a different search term or clear your search.");
        public string NoCompletedProjects => S("noCompletedProjectsText", "No completed projects");
        public string NoArchivedProjects => S("noArchivedProjectsText", "No archived projects");
        public string FocusEmptyTitle => S("focusEmptyTitle", "Nothing needs attention this week");
        public string FocusEmptyDescription => S("focusEmptyDescription", "You're clear for now. Browse all projects to plan ahead or create something new.");
        public string AskForExtensionSection => S("askForExtensionSectionText", "Ask for an extension");
        public string StartNowSection => S("startNowSectionText", "Still achievable - start now");
        public string BackBurnerSection => S("backBurnerSectionText", "Backburner - plenty of time");
        public string OvercommittedTitle => S("overcommittedTitle", "This plan is overloaded");
        public string OvercommittedDescription => S("overcommittedDescription", "There's more work here than the time available. An extension on these would ease the squeeze: <PROJECTS>.");
        public string OvercommittedDescriptionFallback => S("overcommittedDescriptionFallback", "There's more work here than the time available. An extension on your highest-priority projects would ease the squeeze.");
        public string OvercommittedAndMore => S("overcommittedAndMoreText", "and <COUNT> more");
        public string NextTask => S("nextTaskText", "Next");
        public string NextTaskEmpty => S("nextTaskEmptyText", "No effort estimate — add tasks to plan next steps");
        public string DueWithDate => S("dueWithDateText", "Due: <DATE>");
        public string SetDueDate => S("setDueDateText", "Set due date");
        public string RequiredHours => S("requiredHoursText", "<DURATION> left");
        public string RecommendedDaily => S("recommendedDailyText", "<DURATION>/day to finish");
        public string MembersCount => S("membersCountText", "<COUNT> members");
        public string TasksCount => S("tasksCountText", "<COUNT> tasks");
        public string TasksToDoCount => S("tasksToDoCountText", "<COUNT> to do");
        public string MemberBadge => S("memberBadgeText", "Member");
        public string BackToProjects => S("backToProjectsText", "Back to Projects");
        public string ProjectNotFound => S("projectNotFoundText", "Project not found");
        public string ShowInTaskList => S("showInTaskListText", "Show in task list");
        public string ListView => S("listViewText", "List");
        public string BoardView => S("boardViewText", "Board");
        public string NoTasksYet => S("noTasksYetText", "No tasks yet");
        public string CreateFirstTask => S("createFirstTaskText", "Create your first task to get started");
        public string NoTasksInColumn => S("noTasksInColumnText", "No tasks");
        public string MoveTo => S("moveToText", "Move to");
        public string ColumnToDo => S("columnToDoText", "To Do");
        public string ColumnInProgress => S("columnInProgressText", "In Progress");
        public string ColumnDone => S("columnDoneText", "Done");
        public string AddTask => S("addTaskText", "Add task");
        public string MarkCompleteProject => S("markCompleteProjectText", "Mark complete");
        public string ArchiveProject => S("archiveProjectText", "Archive project");
        public string RestoreProject => S("restoreProjectText", "Restore to active");
        public string ProjectMarkedComplete => S("projectMarkedCompleteText", "Project marked complete");
        public string ProjectArchived => S("projectArchivedText", "Project archived");
        public string ProjectRestored => S("projectRestoredText", "Project restored to active");
        public string ProjectCompletedBanner => S("projectCompletedBannerText", "This project is complete. It's hidden from your active list and portfolio.");
        public string ProjectArchivedBanner => S("projectArchivedBannerText", "This project is archived. It's hidden from your active list and portfolio.");
        public string DeleteProject => S("deleteProjectText", "Delete Project");
        public string DeleteProjectConfirm => S("deleteProjectConfirmText", "Are you sure you want to delete this project? This action cannot be undone.");
        public string ProjectDeleted => S("projectDeletedText", "Project deleted successfully");
        public string CouldNotDeleteProject => S("couldNotDeleteProjectText", "Could not delete project");
        public string ProjectCreated => S("projectCreatedText", "Project created successfully");
        public string ProjectUpdated => S("projectUpdatedText", "Project updated successfully");
        public string CouldNotUpdateProject => S("couldNotUpdateProjectText", "Could not update project");
        public string CouldNotLoadProjects => S("couldNotLoadProjectsText", "Could not load your projects");
        public string MembersWithCount => S("membersWithCountText", "Members (<COUNT>)");
        public string You => S("youText", "You");
        public string ProjectOwner => S("projectOwnerText", "Project Owner");
        public string UnknownUser => S("unknownUserText", "Unknown User");
        public string RoleOwner => S("roleOwnerText", "Owner");
        public string RoleAdmin => S("roleAdminText", "Admin");
        public string RoleMember => S("roleMemberText", "Member");
        public string InvitationPendingStatus => S("invitationPendingStatusText", "Pending");
        public string EditOnWeb => S("editOnWebText", "Edit on the web");

        public string ProjectStatusLabel(ProjectLifecycleStatus status)
        {
            switch (status)
            {
                case ProjectLifecycleStatus.Completed: return ProjectStatusCompleted;
                case ProjectLifecycleStatus.Archived: return ProjectStatusArchived;
                default: return ProjectStatusActive;
            }
        }

        public string BucketLabel(PortfolioBucket bucket)
        {
            switch (bucket)
            {
                case PortfolioBucket.OnTrack: return S("bucketOnTrackText", "On track");
                case PortfolioBucket.AtRisk: return S("bucketAtRiskText", "At risk");
                case PortfolioBucket.Critical: return S("bucketCriticalText", "Critical");
                default: return S("bucketNoDeadlineText", "No deadline");
            }
        }

        /// <summary>A badge's words, or null for one this build doesn't know (shown as nothing).</summary>
        public string BadgeLabel(PortfolioBadge badge)
        {
            switch (badge)
            {
                case PortfolioBadge.Overcommitted: return S("badgeOvercommittedText", "Overloaded");
                case PortfolioBadge.StartNow: return S("badgeStartNowText", "Start now");
                case PortfolioBadge.Sacrifice: return S("badgeSacrificeText", "Consider dropping");
                case PortfolioBadge.Unscheduled: return S("badgeUnscheduledText", "Not scheduled");
                case PortfolioBadge.PushedBack: return S("badgePushedBackText", "Pushed back");
                default: return null;
            }
        }

        public string SectionTitle(PortfolioActionSection section)
        {
            switch (section)
            {
                case PortfolioActionSection.AskForExtension: return AskForExtensionSection;
                case PortfolioActionSection.StartNow: return StartNowSection;
                default: return BackBurnerSection;
            }
        }

        public IDictionary<ToDoStatus, string> ColumnTitles() => new Dictionary<ToDoStatus, string>
        {
            { ToDoStatus.NotStarted, ColumnToDo },
            { ToDoStatus.InProgress, ColumnInProgress },
            { ToDoStatus.Completed, ColumnDone },
        };

        public string RoleLabel(ProjectMemberRole? role)
        {
            switch (role)
            {
                case ProjectMemberRole.Owner: return RoleOwner;
                case ProjectMemberRole.Admin: return RoleAdmin;
                default: return RoleMember;
            }
        }

        public IList<string> TaskCompletedPhrases =>
            S("taskCompletedPhrases", "Woah|Cool|Awesome|Incredible|Fantastic")
                .Split(ListSeparator).Select(phrase => phrase.Trim()).Where(phrase => phrase.Length > 0).ToList();

        public string StatusLabel(ToDoStatus status)
        {
            switch (status)
            {
                case ToDoStatus.InProgress: return StatusInProgress;
                case ToDoStatus.Completed: return StatusCompleted;
                default: return StatusNotStarted;
            }
        }

        public string QuadrantLabel(EisenhowerQuadrant quadrant)
        {
            switch (quadrant)
            {
                case EisenhowerQuadrant.Decide: return QuadrantDecide;
                case EisenhowerQuadrant.Delegate: return QuadrantDelegate;
                case EisenhowerQuadrant.Delete: return QuadrantDelete;
                default: return QuadrantDo;
            }
        }

        public string QuadrantSubtitle(EisenhowerQuadrant quadrant)
        {
            switch (quadrant)
            {
                case EisenhowerQuadrant.Decide: return QuadrantImportantNotUrgent;
                case EisenhowerQuadrant.Delegate: return QuadrantNotImportantUrgent;
                case EisenhowerQuadrant.Delete: return QuadrantNotImportantNotUrgent;
                default: return QuadrantUrgentImportant;
            }
        }

        /// <summary>The Mac's one definition of the outcome scale: a /10 wire value to its words.</summary>
        public string OutcomeLabel(int value)
        {
            return StepLabel(new[]
            {
                Tuple.Create(1, S("outcomeBand1Text", "Can postpone a year or more")),
                Tuple.Create(4, S("outcomeBand2Text", "Can postpone a few months")),
                Tuple.Create(6, S("outcomeBand3Text", "Can postpone a few weeks")),
                Tuple.Create(8, S("outcomeBand4Text", "Can postpone a few days")),
                Tuple.Create(10, S("outcomeBand5Text", "Hard deadline — can't shift")),
            }, value);
        }

        public string SetOutcome => S("setOutcomeText", "Set outcome");
        public string EditOutcome => S("editOutcomeText", "Edit outcome");

        /// <summary>The outcome picker's empty row (Mac <c>ForecastText[.notSet]</c>).</summary>
        public string OutcomeNotSet => Forecast("notSet", "Not set");

        /// <summary>
        /// The project page's outcome chip, as the Mac's: "4 / 5 · Can postpone a few days", or "Set outcome".
        /// </summary>
        public string OutcomeChipLabel(int? wire)
        {
            int? step = ProjectOutcomeEdit.ChipStep(wire);
            return step.HasValue ? OutcomeOptionLabel(step.Value) : SetOutcome;
        }

        /// <summary>One row of the outcome picker; a legacy odd value reads "Current: 3.5 / 5 · Can postpone a few weeks".</summary>
        public string OutcomePickerRow(OutcomeOption option)
        {
            if (!option.Wire.HasValue) return OutcomeNotSet;
            if (option.IsLegacy)
                return Forecast("currentOutcome", "Current: <VALUE>").Replace("<VALUE>", ProjectOutcomeEdit.ValueText(option.Wire.Value, Culture))
                       + " · " + OutcomeLabel(option.Wire.Value);
            return OutcomeOptionLabel(option.Step ?? 1);
        }

        private string Forecast(string key, string fallback)
        {
            var value = _forecast != null ? _forecast[key] as JValue : null;
            string text = value != null && value.Type == JTokenType.String ? (string)value : null;
            return string.IsNullOrEmpty(text) ? fallback : text;
        }

        /// <summary>"4 / 5 · Can postpone a few days" — one row of the outcome picker.</summary>
        public string OutcomeOptionLabel(int displayStep)
        {
            int step = Math.Min(ToDoScoring.OutcomeSteps, Math.Max(1, displayStep));
            return step + " / " + ToDoScoring.OutcomeSteps + " · " + OutcomeLabel(step * ToDoScoring.OutcomeWireFactor);
        }

        /// <summary>Web <c>getPerspirationSteps</c>.</summary>
        public string PerspirationLabel(int value)
        {
            return StepLabel(new[]
            {
                Tuple.Create(1, S("perspirationFiveMinutesText", "5 minutes job")),
                Tuple.Create(2, S("perspirationFifteenMinutesText", "15 minutes work")),
                Tuple.Create(3, S("perspirationHalfAnHourText", "Half an hour")),
                Tuple.Create(4, S("perspirationAnHourText", "An hour")),
                Tuple.Create(5, S("perspirationHalfADayText", "Half a day")),
                Tuple.Create(7, S("perspirationADayText", "A day")),
                Tuple.Create(9, S("perspirationAWeekText", "A week")),
            }, value);
        }

        private static string StepLabel(Tuple<int, string>[] steps, int value)
        {
            int clamped = ToDoScoring.ClampLevel(value);
            var match = steps.LastOrDefault(step => step.Item1 <= clamped) ?? steps[0];
            return match.Item2;
        }

        public string DueLabel(string isoString, DateTime now) =>
            ToDoDates.DueLabel(isoString, now, Today, Tomorrow, Yesterday, Culture);

        /// <summary>"Sep 1, 2026", the row's Created cell.</summary>
        public string Abbreviated(string isoString)
        {
            DateTime? date = ToDoDates.Parse(isoString);
            return date.HasValue ? date.Value.ToString("MMM d, yyyy", Culture) : "";
        }

        internal static System.Globalization.CultureInfo Culture
        {
            get
            {
                try { return System.Globalization.CultureInfo.GetCultureInfo(AppSettings.GetInstance.sLangCode ?? "en"); }
                catch (System.Globalization.CultureNotFoundException) { return System.Globalization.CultureInfo.InvariantCulture; }
            }
        }

        /// <summary>
        /// A synced project's name can come through as its provider's raw id; show "Synced project …ab12"
        /// rather than a wall of hex, as the Mac does.
        /// </summary>
        public string ProjectName(string raw)
        {
            string name = (raw ?? "").Trim();
            if (name.Length < 12 || name.Contains(" ")) return name;
            bool looksLikeId = name.All(c => Uri.IsHexDigit(c) || c == '-') && name.Any(char.IsDigit);
            return looksLikeId ? SyncedProject + " …" + name.Substring(name.Length - 4) : name;
        }
    }
}
