#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Client.Data.BMD;
using Client.Main.Core.Client;
using Client.Main.Core.Utilities;
using Client.Main.Models;
using Microsoft.Xna.Framework;

namespace Client.Main.Controls.UI.Game.Skills
{
    /// <summary>
    /// Popup panel displaying all available skills in a grid layout.
    /// Allows player to select a skill for the quick slot.
    /// </summary>
    public class SkillSelectionPanel : UIControl
    {
        private const int COLUMNS = 5;
        private const int PADDING = 8;
        private const int HEADER_HEIGHT = 36;
        private const int DETAIL_WIDTH = 220;
        private const int DETAIL_PADDING = 12;

        private readonly List<SkillSlotControl> _skillSlots = new();
        private readonly LabelControl _titleLabel;
        private readonly UIControl _detailPanel;
        private readonly LabelControl _detailNameLabel;
        private readonly LabelControl _detailTypeLabel;
        private readonly LabelControl _detailStatsLabel;
        private ushort? _selectedSkillId;

        private sealed class PanelControl : UIControl { }

        /// <summary>
        /// Fired when a skill is selected from the panel.
        /// </summary>
        public event Action<SkillEntryState>? SkillSelected;

        public SkillSelectionPanel()
        {
            Interactive = true;
            BackgroundColor = new Color(20, 20, 30) * 0.95f;
            BorderColor = Color.Gold;
            BorderThickness = 2;
            Visible = false;

            // Center on screen
            Align = ControlAlign.HorizontalCenter | ControlAlign.VerticalCenter;

            // Title
            _titleLabel = new LabelControl
            {
                Text = "Select Skill",
                TextColor = Color.Gold,
                X = PADDING,
                Y = PADDING,
                ViewSize = new Point(320, 22),
                Align = ControlAlign.HorizontalCenter
            };
            Controls.Add(_titleLabel);

            _detailPanel = new PanelControl
            {
                AutoViewSize = false,
                ControlSize = new Point(DETAIL_WIDTH, 220),
                ViewSize = new Point(DETAIL_WIDTH, 220),
                BackgroundColor = new Color(12, 16, 28) * 0.95f,
                BorderColor = new Color(70, 70, 110),
                BorderThickness = 1,
                Interactive = false
            };
            Controls.Add(_detailPanel);

            _detailNameLabel = new LabelControl
            {
                Text = "Skill Info",
                TextColor = Color.White,
                FontSize = 14f,
                X = DETAIL_PADDING,
                Y = DETAIL_PADDING,
                ViewSize = new Point(DETAIL_WIDTH - DETAIL_PADDING * 2, 24)
            };
            _detailPanel.Controls.Add(_detailNameLabel);

            _detailTypeLabel = new LabelControl
            {
                Text = string.Empty,
                TextColor = Color.Gold,
                FontSize = 12f,
                X = DETAIL_PADDING,
                Y = DETAIL_PADDING + 24,
                ViewSize = new Point(DETAIL_WIDTH - DETAIL_PADDING * 2, 20)
            };
            _detailPanel.Controls.Add(_detailTypeLabel);

            _detailStatsLabel = new LabelControl
            {
                Text = "Hover a skill to see details.",
                TextColor = Color.Silver,
                X = DETAIL_PADDING,
                Y = DETAIL_PADDING + 46,
                ViewSize = new Point(DETAIL_WIDTH - DETAIL_PADDING * 2, 150),
                Scale = 0.85f
            };
            _detailPanel.Controls.Add(_detailStatsLabel);
        }

        /// <summary>
        /// Opens the panel and populates it with the character's skills.
        /// </summary>
        public void Open(CharacterState characterState)
        {
            if (characterState == null)
                return;

            var skills = characterState
                .GetSkills()
                .OrderBy(s => SkillDatabase.GetSkillName(s.SkillId))
                .ThenBy(s => s.SkillId)
                .ToList();

            // Update title with skill count
            _titleLabel.Text = $"Select Skill ({skills.Count} available)";

            // Clear existing skill slots
            foreach (var slot in _skillSlots)
            {
                slot.HoverChanged -= OnSkillSlotHover;
                Controls.Remove(slot);
            }
            _skillSlots.Clear();

            // Calculate panel size
            int rows = (int)Math.Ceiling(skills.Count / (float)COLUMNS);
            if (rows == 0) rows = 1; // At least one row even if no skills

            int gridWidth = (COLUMNS * SkillSlotControl.SLOT_WIDTH) + ((COLUMNS + 1) * PADDING);
            int gridHeight = HEADER_HEIGHT + PADDING + (rows * (SkillSlotControl.SLOT_HEIGHT + PADDING)) + PADDING;

            int totalWidth = gridWidth + DETAIL_WIDTH + (PADDING * 3);
            int totalHeight = gridHeight + PADDING;

            ViewSize = new Point(totalWidth, totalHeight);
            ControlSize = ViewSize;

            _titleLabel.ViewSize = new Point(totalWidth - (PADDING * 2), 24);
            _titleLabel.X = PADDING;

            // Create skill slots in grid
            for (int i = 0; i < skills.Count; i++)
            {
                int row = i / COLUMNS;
                int col = i % COLUMNS;

                var slot = new SkillSlotControl
                {
                    Skill = skills[i],
                    X = PADDING + (col * (SkillSlotControl.SLOT_WIDTH + PADDING)),
                    Y = HEADER_HEIGHT + PADDING + (row * (SkillSlotControl.SLOT_HEIGHT + PADDING)),
                    IsTooltipEnabled = false
                };

                slot.Click += (sender, args) => OnSkillSlotClicked(slot);
                slot.HoverChanged += OnSkillSlotHover;
                slot.IsSelected = _selectedSkillId.HasValue && slot.Skill?.SkillId == _selectedSkillId.Value;
                _skillSlots.Add(slot);
                Controls.Add(slot);
            }

            // Position detail panel
            int detailHeight = Math.Max(gridHeight - HEADER_HEIGHT - (PADDING * 2), 160);
            _detailPanel.X = gridWidth + (PADDING * 2);
            _detailPanel.Y = HEADER_HEIGHT;
            _detailPanel.ControlSize = new Point(DETAIL_WIDTH, detailHeight);
            _detailPanel.ViewSize = _detailPanel.ControlSize;

            int statsHeight = Math.Max(detailHeight - (DETAIL_PADDING + 46), 40);
            _detailStatsLabel.ViewSize = new Point(DETAIL_WIDTH - DETAIL_PADDING * 2, statsHeight);

            if (_selectedSkillId.HasValue)
            {
                HighlightSkill(_selectedSkillId.Value);
            }
            else
            {
                UpdateDetail(skills.FirstOrDefault());
            }

            Visible = true;
            BringToFront();
        }

        /// <summary>
        /// Closes the panel.
        /// </summary>
        public void Close()
        {
            Visible = false;
        }

        private void OnSkillSlotClicked(SkillSlotControl slot)
        {
            if (slot.Skill == null)
                return;

            _selectedSkillId = slot.Skill.SkillId;
            SkillSelected?.Invoke(slot.Skill);
            Close();
        }

        public void HighlightSkill(ushort skillId)
        {
            _selectedSkillId = skillId;

            SkillEntryState? selected = null;
            foreach (var slot in _skillSlots)
            {
                bool isMatch = slot.Skill?.SkillId == skillId;
                slot.IsSelected = isMatch;
                if (isMatch)
                {
                    selected = slot.Skill;
                }
            }

            UpdateDetail(selected);
        }

        private void OnSkillSlotHover(SkillEntryState? skill)
        {
            if (!_selectedSkillId.HasValue)
            {
                UpdateDetail(skill);
                return;
            }

            if (skill != null)
            {
                UpdateDetail(skill);
                return;
            }

            var selectedSlot = _skillSlots.FirstOrDefault(s => s.Skill?.SkillId == _selectedSkillId);
            UpdateDetail(selectedSlot?.Skill);
        }

        private void UpdateDetail(SkillEntryState? skill)
        {
            if (skill == null)
            {
                _detailNameLabel.Text = "Skill Info";
                _detailTypeLabel.Text = string.Empty;
                _detailStatsLabel.Text = "Hover a skill to see details.";
                _detailStatsLabel.TextColor = Color.Silver;
                return;
            }

            var definition = SkillDatabase.GetSkillDefinition(skill.SkillId);
            var type = SkillDatabase.GetSkillType(skill.SkillId);

            string typeText = type switch
            {
                SkillType.Area => "Area",
                SkillType.Self => "Self",
                _ => "Target"
            };

            _detailNameLabel.Text = SkillDatabase.GetSkillName(skill.SkillId);
            _detailTypeLabel.Text = $"Type: {typeText}  •  Level {skill.SkillLevel}";

            var sb = new StringBuilder();
            sb.AppendLine($"Skill ID: {skill.SkillId}");

            if (definition != null)
            {
                if (definition.RequiredLevel > 0)
                {
                    sb.AppendLine($"Required Level: {definition.RequiredLevel}");
                }
                if (definition.RequiredStrength > 0)
                {
                    sb.AppendLine($"Required Strength: {definition.RequiredStrength}");
                }
                if (definition.RequiredDexterity > 0)
                {
                    sb.AppendLine($"Required Dexterity: {definition.RequiredDexterity}");
                }
                if (definition.RequiredEnergy > 0)
                {
                    sb.AppendLine($"Required Energy: {definition.RequiredEnergy}");
                }
                if (definition.RequiredLeadership > 0)
                {
                    sb.AppendLine($"Required Command: {definition.RequiredLeadership}");
                }

                if (definition.ManaCost > 0 || definition.AbilityGaugeCost > 0)
                {
                    sb.Append("Cost: ");
                    if (definition.ManaCost > 0)
                    {
                        sb.Append($"Mana {definition.ManaCost}");
                    }
                    if (definition.AbilityGaugeCost > 0)
                    {
                        if (definition.ManaCost > 0)
                        {
                            sb.Append(" | ");
                        }
                        sb.Append($"AG {definition.AbilityGaugeCost}");
                    }
                    sb.AppendLine();
                }

                if (definition.Damage > 0)
                {
                    sb.AppendLine($"Base Damage: {definition.Damage}");
                }
                if (definition.Distance > 0)
                {
                    sb.AppendLine($"Range: {definition.Distance}");
                }
                if (definition.Delay > 0)
                {
                    sb.AppendLine($"Cooldown: {definition.Delay} ms");
                }
            }

            if (sb.Length == 0)
            {
                sb.Append("No additional data available.");
            }

            _detailStatsLabel.Text = sb.ToString();
            _detailStatsLabel.TextColor = Color.WhiteSmoke;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            // Close on click outside (optional - can be enabled if desired)
            // if (Visible && !IsMouseOver && CurrentMouseState.LeftButton == ButtonState.Pressed)
            // {
            //     Close();
            // }
        }
    }
}
