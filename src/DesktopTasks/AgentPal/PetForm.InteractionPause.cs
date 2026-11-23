// SPDX-License-Identifier: Apache-2.0
using System;
using System.Windows.Forms;

namespace AgentPal
{
    internal sealed partial class PetForm
    {
        private bool interactionPaused;
        private bool replyPendingAfterEditing;

        private static bool IsOpenEditor(Form form)
        {
            return form != null && form.Visible && form.WindowState != FormWindowState.Minimized;
        }

        private bool IsEditingInteraction()
        {
            return QuickTaskWindow.IsForegroundEditor || IsOpenEditor(defaultSettingsWindow) || IsOpenEditor(claudeTaskForm) ||
                   IsOpenEditor(reminderForm) || IsOpenEditor(workshopForm) ||
                   IsOpenEditor(promptForm) || IsOpenEditor(activityTray) ||
                   characterPackDialogOpen || choosingFlightArtwork ||
                   clickActionsMenu.Visible || (ContextMenuStrip != null && ContextMenuStrip.Visible) ||
                   (IsHandleCreated && !Enabled);
        }

        private bool PauseForEditing()
        {
            if (IsEditingInteraction())
            {
                lastAnimationTick = DateTime.UtcNow;
                if (!interactionPaused)
                {
                    interactionPaused = true;
                    CancelWander();
                    if (!dragging && IsLocomotionState(state)) SetState("idle");
                    clickDelayTimer.Stop();
                    quoteBubble.Hide();
                    if (replyBubble.Visible && !replyBubble.UserDismissed)
                    {
                        replyPendingAfterEditing = true;
                        replyBubble.Hide();
                    }
                    if (Visible) RefreshPetVisual();
                }
                return !dragging;
            }

            if (interactionPaused)
            {
                interactionPaused = false;
                animationAccumulatorMs = 0;
                ScheduleNextIdleAction();
                ScheduleNextWander();
                if (Visible) RefreshPetVisual();
            }
            if (replyPendingAfterEditing && reminderOverlay == null && reminderQueue.Count == 0)
            {
                replyPendingAfterEditing = false;
                ShowReplyBubble();
            }
            return false;
        }
    }
}
