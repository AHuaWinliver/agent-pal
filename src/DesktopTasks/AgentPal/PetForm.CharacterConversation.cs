using System;

namespace AgentPal
{
    internal sealed partial class PetForm
    {
        private ActivityRecord BeginCharacterConversation(PetCharacter character, string directory)
        {
            ActivityRecord record = activities.Find(delegate(ActivityRecord item)
            {
                return item.CharacterConversation && String.Equals(item.CharacterId, character.Id, StringComparison.OrdinalIgnoreCase);
            });
            if (record == null)
            {
                record = new ActivityRecord { CharacterId = character.Id, CharacterConversation = true, Output = "" };
                activities.Insert(0, record);
            }
            if (!String.Equals(record.WorkingDirectory, directory, StringComparison.OrdinalIgnoreCase)) record.SessionId = null;
            record.Title = character.Name;
            record.WorkingDirectory = directory;
            record.State = "正在运行";
            record.Updated = DateTime.Now;
            record.Unread = false;
            currentActivity = lastReplyActivity = record;
            while (activities.Count > 30)
            {
                int remove = activities.FindLastIndex(delegate(ActivityRecord item) { return !item.CharacterConversation; });
                if (remove < 0) break;
                activities.RemoveAt(remove);
            }
            return record;
        }
    }
}
