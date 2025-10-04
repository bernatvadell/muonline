#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Client.Main.Core.Client;
using Microsoft.Xna.Framework;

namespace Client.Main.Controls.UI.Game.Buffs
{
    /// <summary>
    /// Panel displaying all active buffs in the top-left corner of the game screen.
    /// Shows buffs in a horizontal row without borders.
    /// </summary>
    public class ActiveBuffsPanel : UIControl
    {
        private readonly CharacterState _characterState;
        private readonly List<BuffSlotControl> _buffSlots = new();
        private const int MAX_VISIBLE_BUFFS = 10;
        private const int BUFF_SPACING = 4;

        public ActiveBuffsPanel(CharacterState characterState)
        {
            _characterState = characterState;

            AutoViewSize = false;
            Interactive = false;

            // Position in top-left corner (with small margin)
            X = 10;
            Y = 10;

            // Initial size (will be adjusted based on active buffs)
            ControlSize = new Point(0, BuffSlotControl.SLOT_SIZE);
            ViewSize = ControlSize;

            // No background or border
            BackgroundColor = Color.Transparent;
            BorderColor = Color.Transparent;
            BorderThickness = 0;

            // Create buff slot controls
            for (int i = 0; i < MAX_VISIBLE_BUFFS; i++)
            {
                var slot = new BuffSlotControl
                {
                    X = i * (BuffSlotControl.SLOT_SIZE + BUFF_SPACING),
                    Y = 0,
                    Visible = false
                };
                _buffSlots.Add(slot);
                Controls.Add(slot);
            }

            // Subscribe to buff changes
            _characterState.ActiveBuffsChanged += OnActiveBuffsChanged;

            // Initial update
            UpdateBuffDisplay();
        }

        private void OnActiveBuffsChanged()
        {
            // Schedule on main thread since this event comes from network thread
            MuGame.ScheduleOnMainThread(UpdateBuffDisplay);
        }

        private void UpdateBuffDisplay()
        {
            var activeBuffs = _characterState.GetActiveBuffs().Take(MAX_VISIBLE_BUFFS).ToList();

            // Update each slot
            for (int i = 0; i < _buffSlots.Count; i++)
            {
                if (i < activeBuffs.Count)
                {
                    _buffSlots[i].Buff = activeBuffs[i];
                    _buffSlots[i].Visible = true;
                }
                else
                {
                    _buffSlots[i].Buff = null;
                    _buffSlots[i].Visible = false;
                }
            }

            // Adjust panel width based on number of active buffs
            int visibleCount = activeBuffs.Count;
            if (visibleCount > 0)
            {
                int newWidth = visibleCount * BuffSlotControl.SLOT_SIZE + (visibleCount - 1) * BUFF_SPACING;
                ControlSize = new Point(newWidth, BuffSlotControl.SLOT_SIZE);
                ViewSize = ControlSize;
            }
            else
            {
                ControlSize = new Point(0, BuffSlotControl.SLOT_SIZE);
                ViewSize = ControlSize;
            }
        }

        public override void Dispose()
        {
            // Unsubscribe from events
            _characterState.ActiveBuffsChanged -= OnActiveBuffsChanged;
            base.Dispose();
        }
    }
}
