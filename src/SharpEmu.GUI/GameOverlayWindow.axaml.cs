// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using Avalonia.Controls;

namespace SharpEmu.GUI;

/// <summary>
/// Borderless owned window hosting the PS-button in-game overlay. All logic
/// (tile actions, gamepad navigation, status updates) lives in
/// <see cref="MainWindow"/>, which reaches the named controls directly; this
/// class only exists to host the XAML content above the native game surface.
/// </summary>
public partial class GameOverlayWindow : Window
{
    public GameOverlayWindow()
    {
        InitializeComponent();
    }
}
