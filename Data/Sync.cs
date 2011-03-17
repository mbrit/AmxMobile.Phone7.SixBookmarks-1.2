using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace AmxMobile.Phone7.SixBookmarks
{
    public class Sync
    {
        private Action Callback { get; set; }
        private Failed Failed { get; set; }
        private List<SyncWorkItem> WorkItems { get; set; }
        private int WorkItemIndex { get; set; }

        public Sync()
        {
            this.WorkItems = new List<SyncWorkItem>();
        }

        public void DoSync(Action callback, Failed failed)
        {
            // set...
            this.Callback = callback;
            this.Failed = failed;

            // get latest...
            this.PushChanges();
        }

        private void PushChanges()
        {
            // need to get all from the server - we need to calculate a delta...
            BookmarksService service = new BookmarksService();
            service.GetAll<Bookmark>((Action<List<Bookmark>>)delegate(List<Bookmark> fromServer)
            {
                // get the local set...
                List<Bookmark> updates = Bookmark.GetBookmarksForServerUpdate();
                List<Bookmark> deletes = Bookmark.GetBookmarksForServerDelete();

                // et...
                EntityType et = EntityType.GetEntityType(typeof(Bookmark));
                if (et == null)
                    throw new InvalidOperationException("'et' is null.");

                // reset the work items...
                this.WorkItems = new List<SyncWorkItem>();

                // we do have changes, so get the latest from the server...
                foreach (Bookmark local in updates)
                {
                    // find it in our set...
                    Bookmark toUpdate = null;
                    foreach (Bookmark server in fromServer)
                    {
                        if (local.Ordinal == server.Ordinal)
                        {
                            toUpdate = server;
                            break;
                        }
                    }

                    // did we have one to change?
                    if (toUpdate != null)
                    {
                        // walk the fields...
                        int serverId = 0;
                        foreach (EntityField field in et.Fields)
                        {
                            if (!(field.IsKey))
                                toUpdate.SetValue(field, local.GetValue(field), SetReason.UserSet);
                            else
                                serverId = toUpdate.BookmarkId;
                        }

                        // send that up...
                        this.WorkItems.Add(new SyncWorkItem(ODataOperation.Update, toUpdate, serverId));
                    }
                    else
                    {
                        // we need to insert it...
                        this.WorkItems.Add(new SyncWorkItem(ODataOperation.Insert, local, 0));
                    }
                }

                // what about ones to delete?
                foreach (Bookmark local in deletes)
                {
                    // find a matching ordinal on the server...
                    foreach (Bookmark server in fromServer)
                    {
                        if (local.Ordinal == server.Ordinal)
                            this.WorkItems.Add(new SyncWorkItem(ODataOperation.Delete, server, server.BookmarkId));
                    }
                }

                // reset the queue and run it...
                this.WorkItemIndex = 0;
                this.PushNextWorkItem();

            }, this.Failed);
        }

        private void PushNextWorkItem()
        {
            Debug.WriteLine(string.Format("Pushing work item {0}...", this.WorkItemIndex));

            // have we reached the end?  if so, branch off and get the latest...
            if (this.WorkItemIndex == this.WorkItems.Count)
            {
                this.GetLatest();
                return;
            }

            // get it...
            SyncWorkItem item = this.WorkItems[this.WorkItemIndex];

            // callback...
            Action callback = new Action(HandleWorkItemCompleted);

            // run...
            BookmarksService service = new BookmarksService();
            if (item.Operation == ODataOperation.Insert)
                service.PushInsert(item.Bookmark, callback, this.Failed);
            else if (item.Operation == ODataOperation.Update)
                service.PushUpdate(item.Bookmark, item.ServerId, callback, this.Failed);
            else if (item.Operation == ODataOperation.Delete)
                service.PushDelete(item.Bookmark, item.ServerId, callback, this.Failed);
            else
                throw new NotSupportedException(string.Format("Cannot handle '{0}'.", item.Operation));
        }

        private void HandleWorkItemCompleted()
        {
            Debug.WriteLine("Work item completed.");

            // increment the index...
            this.WorkItemIndex++;

            // run the next one...
            this.PushNextWorkItem();
        }

        private void GetLatest()
        {
            Debug.WriteLine("Getting latest...");

            BookmarksService service = new BookmarksService();
            service.GetAll((Action<List<Bookmark>>)delegate(List<Bookmark> bookmarks)
            {
                // delete first...
                Bookmark.DeleteAll();

                // go through and save them...
                foreach (Bookmark fromServer in bookmarks)
                {
                    // we need to clone it as the ones that come from the server will have an id set.  we
                    // need to junk this id...
                    Bookmark newBookmark = new Bookmark();
                    newBookmark.Ordinal = fromServer.Ordinal;
                    newBookmark.Name = fromServer.Name;
                    newBookmark.Url = fromServer.Url;

                    // set the local only stuff...
                    newBookmark.IsLocalModified = false;
                    newBookmark.IsLocalDeleted = false;

                    // save...
                    newBookmark.SaveChanges();
                }

                // signal that we've finished...
                this.Callback();

            }, this.Failed);
        }
    }
}
