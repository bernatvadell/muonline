#nullable enable
using System;
using Client.Main.Core.Client;
using Client.Main.Core.Utilities;
using Client.Main.Models;
using Microsoft.Xna.Framework;

namespace Client.Main.Controls.UI.Game.Skills
{
    /// <summary>
    /// Single skill slot display - shows skill icon and basic info.
    /// </summary>
    public class SkillSlotControl : UIControl
    {
        private SkillEntryState? _skill;
        private readonly LabelControl _skillIdLabel;
        private readonly LabelControl _skillLevelLabel;
        private readonly LabelControl _tooltipLabel;
        private bool _isSelected;
        private bool _wasHovered;

        public const int SLOT_WIDTH = 28;
        public const int SLOT_HEIGHT = 48;

        public SkillEntryState? Skill
        {
            get => _skill;
            set
            {
                _skill = value;
                UpdateDisplay();
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                UpdateDisplay();
            }
        }

        public bool IsTooltipEnabled { get; set; } = true;

        public event Action<SkillEntryState?>? HoverChanged;

        public SkillSlotControl()
        {
            AutoViewSize = false;
            ControlSize = new Point(SLOT_WIDTH, SLOT_HEIGHT);
            ViewSize = ControlSize;
            Interactive = true;

            // Skill ID label (centered)
            _skillIdLabel = new LabelControl
            {
                Text = "?",
                TextColor = Color.White,
                X = 2,
                Y = 8,
                ViewSize = new Point(SLOT_WIDTH - 4, 22),
                Align = ControlAlign.HorizontalCenter
            };
            Controls.Add(_skillIdLabel);

            // Skill level label (bottom right corner)
            _skillLevelLabel = new LabelControl
            {
                Text = "",
                TextColor = Color.Yellow,
                X = Math.Max(0, SLOT_WIDTH - 18),
                Y = SLOT_HEIGHT - 16,
                ViewSize = new Point(16, 16),
                Scale = 0.55f
            };
            Controls.Add(_skillLevelLabel);

            // Tooltip (hidden by default)
            _tooltipLabel = new LabelControl
            {
                Text = "",
                TextColor = Color.White,
                BackgroundColor = new Color(0, 0, 0) * 0.9f,
                BorderColor = Color.Gold,
                BorderThickness = 1,
                X = SLOT_WIDTH + 5,
                Y = 0,
                ViewSize = new Point(200, 80),
                Visible = false,
                Scale = 0.75f
            };
            Controls.Add(_tooltipLabel);

            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            if (_skill != null)
            {
                string skillName = SkillDatabase.GetSkillName(_skill.SkillId);

                // Truncate long names to fit in slot
                if (skillName.Length > 10)
                    skillName = skillName.Substring(0, 9) + "...";

                _skillIdLabel.Text = skillName;
                _skillIdLabel.Visible = true;
                _skillIdLabel.TextColor = Color.White;
                _skillLevelLabel.Text = $"Lv{_skill.SkillLevel}";
                _skillLevelLabel.Visible = true;
            }
            else
            {
                _skillIdLabel.Text = "EMPTY";
                _skillIdLabel.Visible = true;
                _skillIdLabel.TextColor = Color.Gray;
                _skillLevelLabel.Visible = false;
            }

            // Update border/background based on selection
            if (_isSelected)
            {
                BackgroundColor = new Color(255, 215, 0) * 0.5f; // Gold highlight (more visible)
                BorderColor = Color.Gold;
                BorderThickness = 3;
            }
            else
            {
                BackgroundColor = Color.Black * 0.7f;
                BorderColor = Color.Gray;
                BorderThickness = 1;
            }
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            // Hover effect and tooltip
            bool isHovered = IsMouseOver;

            if (!IsTooltipEnabled)
            {
                _tooltipLabel.Visible = false;
            }

            if (isHovered && !_isSelected)
            {
                BackgroundColor = Color.White * 0.2f;
                BorderColor = Color.White;

                // Show tooltip with skill info
                if (IsTooltipEnabled && _skill != null)
                {
                    RenderTooltip();
                }
            }
            else if (!isHovered && !_isSelected)
            {
                BackgroundColor = Color.Black * 0.7f;
                BorderColor = Color.Gray;
                if (IsTooltipEnabled)
                {
                    _tooltipLabel.Visible = false;
                }
            }

            // Also hide tooltip when selected
            if (_isSelected)
            {
                if (IsTooltipEnabled)
                {
                    _tooltipLabel.Visible = false;
                }
            }

            if (_wasHovered != isHovered)
            {
                _wasHovered = isHovered;
                HoverChanged?.Invoke(isHovered ? _skill : null);
            }
        }

        private void RenderTooltip()
        {
            if (_skill == null)
            {
                _tooltipLabel.Visible = false;
                return;
            }

            // Get skill data from SkillDatabase
            var skillDef = SkillDatabase.GetSkillDefinition(_skill.SkillId);
            string skillName = SkillDatabase.GetSkillName(_skill.SkillId);
            ushort manaCost = SkillDatabase.GetSkillManaCost(_skill.SkillId);
            ushort agCost = SkillDatabase.GetSkillAGCost(_skill.SkillId);
            var skillType = SkillDatabase.GetSkillType(_skill.SkillId);

            // Build tooltip text
            string typeText = skillType switch
            {
                Data.BMD.SkillType.Area => "[AREA]",
                Data.BMD.SkillType.Target => "[TARGET]",
                Data.BMD.SkillType.Self => "[SELF]",
                _ => ""
            };

            var tooltip = $"{skillName} {typeText}\nLevel: {_skill.SkillLevel}";

            if (manaCost > 0 || agCost > 0)
            {
                tooltip += $"\nMana: {manaCost}";
                if (agCost > 0)
                {
                    tooltip += $" | AG: {agCost}";
                }
            }

            if (skillDef != null)
            {
                if (skillDef.Damage > 0)
                {
                    tooltip += $"\nDamage: {skillDef.Damage}";
                }

                if (skillDef.RequiredLevel > 0)
                {
                    tooltip += $"\nRequired Lv: {skillDef.RequiredLevel}";
                }
            }

            _tooltipLabel.Text = tooltip;

            _tooltipLabel.Visible = true;
        }
    }
}
