namespace DNNGamification.Components.Controllers
{
    using DNNGamification.Components;
    using DNNGamification.Components.Repositories;
    using DNNGamification.Components.Entities;

    using DotNetNuke.Entities;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Entities.Users;

    using DotNetNuke.Framework;

    using DotNetNuke.Services.Social;
    using DotNetNuke.Services.Social.Notifications;

    using System.Collections.Generic;

    using System;
    using System.Transactions;
    using System.Linq;

    /// <summary>
    /// Mechanics controller.
    /// </summary>
    public class MechanicsController : ServiceLocator<IMechanicsController, MechanicsController>, IMechanicsController
    {
        #region Private Fields

        /// <summary>
        /// Instance of unit of work.
        /// </summary>
        private UnitOfWork _uow = new UnitOfWork();

        #endregion

        #region Protected Methods : Factory

        /// <summary>
        /// Get instance factory.
        /// </summary>
        protected override Func<IMechanicsController> GetFactory()
        {
            return (Func<IMechanicsController>)(() => (IMechanicsController)new MechanicsController());
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Send badge notification.
        /// </summary>
        private MechanicsController Notify(int userId, int portalId, string badgeName)
        {
            var notification = new Notification // define award notification
            {
                IncludeDismissAction = false, SendToast = true
            };

            var badgeAwarded = NotificationsController.Instance.GetNotificationType("DNNGamification_BadgeAwarded");
            {
                notification.NotificationTypeID = badgeAwarded.NotificationTypeId;
            }

            int portalAdminId = new PortalController().GetPortal(portalId).AdministratorId;
            {
                notification.SenderUserID = portalAdminId;
            }

            UserInfo recipient = UserController.GetUserById(portalId, userId);

            if (recipient == null) // check portal user exists
            {
                throw new NullReferenceException(String.Format("User with ID \"{0}\" is not found", userId));
            }

            string subject = "Badge awarded"; string body = String.Format("Congratulations! {0} badge awarded.", badgeName);
            {
                notification.Subject = subject; notification.Body = body;
            }

            NotificationsController.Instance.SendNotification
            (
                notification, portalId, null, new List<UserInfo>() { recipient }
            );

            return this;
        }

        /// <summary>
        /// 
        /// </summary>
        private MechanicsController Log(string synonym, int desktopModuleId, int userId, int portalId)
        {
            int result = -1;

            Activity activity = _uow.Activities.GetBy(synonym, desktopModuleId);
            {
                if (activity == null) throw new NullReferenceException("Activity is not found");
            }

            if (!(activity.Once && _uow.UserActivitiesLog.GetManyBy(userId, portalId).Any(l => l.ActivityId == activity.ActivityId)))
            {
                result = _uow.UserActivitiesLog.Add(activity.ActivityId, userId, portalId, activity.ActivityPoints);
            }

            return this;
        }

        /// <summary>
        /// 
        /// </summary>
        private MechanicsController Award(int userId, int portalId)
        {
            int result = -1;

            var dictionary = new Dictionary<int, int>();

            foreach (var group in _uow.UserActivitiesLog.GetManyBy(userId, portalId).GroupBy(a => a.ActivityId))
            {
                int points = 0; group.ForEach(a => points += a.ActivityPoints);
                {
                    dictionary.Add(group.Key, points); // add activity-point item to dictionary
                }
            }
            foreach (var group in _uow.BadgeActivities.Get(portalId).GroupBy(b => b.BadgeId))
            {
                bool award = true; Badge badge = _uow.Badges.GetBy(group.Key);

                foreach (BadgeActivity current in group) // iterate and count points to award
                {
                    int key = current.ActivityId; // define activity ID group key
                    {
                        award &= dictionary.ContainsKey(key) && dictionary[key] >= current.ActivityPoints;
                    }
                }
                foreach (UserBadge current in _uow.UserBadges.GetManyBy(userId, portalId))
                {
                    if (current.BadgeId == badge.BadgeId && (!current.Expire.HasValue || current.Expire > DateTime.UtcNow))
                    {
                        award = false; break; // check existing user badge
                    }
                }

                if (award) // add new uaser badge and notify
                {
                    result = _uow.UserBadges.Add(badge.BadgeId, userId, portalId);
                    {
                        Notify(userId, portalId, badge.Name);
                    }
                }
            }

            return this;
        }

        #endregion

        #region Public Methods : Insert

        /// <summary>
        /// Adds badge activity.
        /// </summary>
        public int AddBadgeActivity(int portalId, int badgeId, int activityId, int activityPoints)
        {
            using (var r = _uow.BadgeActivities) return r.Add(portalId, activityId, badgeId, activityPoints);
        }

        /// <summary>
        /// Adds activity.
        /// </summary>
        public int AddActivity(int desktopModuleId, string name, string description, string synonym, int activityPoints, bool once)
        {
            using (var r = _uow.Activities) return r.Add(desktopModuleId, name, description, synonym, activityPoints, once);
        }

        /// <summary>
        /// Adds user badge.
        /// </summary>
        public int AddUserBadge(int badgeId, int userId, int portalId)
        {
            using (var r = _uow.UserBadges) return r.Add(badgeId, userId, portalId);
        }

        #endregion

        #region Public Methods : Select

        /// <summary>
        /// Gets badges list.
        /// </summary>
        public List<Badge> GetBadges(int portalId)
        {
            using (var r = _uow.Badges) return r.GetAll(portalId);
        }

        /// <summary>
        /// Gets user badges list.
        /// </summary>
        public List<UserBadge> GetUserBadges(int userId, int portalId, bool actual = true, int? top = null)
        {
            using (var r = _uow.UserBadges) return r.GetManyBy(userId, portalId, actual, top);
        }

        /// <summary>
        /// Gets badge activities list.
        /// </summary>
        public List<BadgeActivity> GetBadgeActivities(int badgeId)
        {
            using (var r = _uow.BadgeActivities) return r.GetManyBy(badgeId);
        }

        /// <summary>
        /// Gets activity by name and desktop module ID.
        /// </summary>
        public Activity GetActivity(string synonym, int desktopModuleId)
        {
            using (var r = _uow.Activities) return r.GetBy(synonym, desktopModuleId);
        }

        /// <summary>
        /// Gets activity by ID.
        /// </summary>
        public Activity GetActivity(int id)
        {
            using (var r = _uow.Activities) return r.GetBy(id);
        }

        #endregion

        #region Public Methods : Update

        /// <summary>
        /// Updates activity.
        /// </summary>
        public void UpdateActivity(int id, int desktopModuleId, string name, string description, string synonym, int activityPoints, bool once)
        {
            using (var r = _uow.Activities) r.Update(id, desktopModuleId, name, description, synonym, activityPoints, once);
        }

        #endregion

        #region Public Methods : Delete

        /// <summary>
        /// Deletes action definition by ID.
        /// </summary>
        public void DeleteActivity(int id)
        {
            using (var r = _uow.Activities) r.Delete(id);
        }

        /// <summary>
        /// Deletes action definition by synonym and module ID.
        /// </summary>
        public void DeleteActivity(string synonym, int desktopModuleId)
        {
            using (var r = _uow.Activities) r.Delete(synonym, desktopModuleId);
        }

        /// <summary>
        /// Deletes user badge.
        /// </summary>
        public void DeleteUserBadge(int id)
        {
            using (var r = _uow.UserBadges) _uow.UserBadges.Delete(id);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Logs user activity (uses transaction).
        /// </summary>
        public void LogUserActivity(string synonym, int desktopModuleId, int userId, int portalId)
        {
            Log(synonym, desktopModuleId, userId, portalId).Award(userId, portalId);
        }

        #endregion

        #region Public Methods : IDisposable

        /// <summary>
        /// Disposes controller.
        /// </summary>
        public void Dispose()
        {
            Dispose(true); GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes controller.
        /// </summary>
        public virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // free managed resources
            }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Default constructor.
        /// </summary>
        public MechanicsController() { }

        #endregion
    }
}
