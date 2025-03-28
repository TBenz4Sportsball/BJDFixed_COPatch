﻿using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.GameContent;

namespace BetterJonasDevices;

internal class NightVisionRenderer: IRenderer
{
    MeshRef quadRef;
    ICoreClientAPI api;
    public IShaderProgram overlayShaderProg;

    GlowingEntities glowingEntities = new();


    public NightVisionRenderer(ICoreClientAPI api)
    {
        this.api = api;

        api.Event.ReloadShader += Load;

        MeshData quadMesh = QuadMeshUtil.GetCustomQuadModelData(-1, -1, 0, 2, 2);
        quadMesh.Rgba = null;

        quadRef = api.Render.UploadMesh(quadMesh);
    }

    public double RenderOrder => 1.1;
    public int RenderRange => 1;

    public bool Load()
    {
        overlayShaderProg = api.Shader.NewShaderProgram();
        overlayShaderProg.VertexShader = api.Shader.NewShader(EnumShaderType.VertexShader);
        overlayShaderProg.FragmentShader = api.Shader.NewShader(EnumShaderType.FragmentShader);

        api.Shader.RegisterFileShaderProgram("nightvisionoverlay", overlayShaderProg);
        return overlayShaderProg.Compile();
    }

    public void Dispose()
    {
        api.Render.DeleteMesh(quadRef);
        overlayShaderProg.Dispose();
    }

    public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
    {
        bool isActive = ApplyEffect();
        if (!isActive && glowingEntities.Count > 0)
        {
            glowingEntities.Clear();
        }
    }

    private bool ApplyEffect()
    {
        var inv = api.World.Player.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName);
        if (inv == null) return false;

        var stack = inv[(int)EnumCharacterDressType.ArmorHead]?.Itemstack;

        /* Combat Overhaul replaces the 3 vanilla armor slots with 24 specialized slots. The night vision helmet can occupy 
         * 3 slots, Outer Head, Outer Face, and Outer Neck, so we have to check all 3. Currently, these slots are at indicies
         * 15, 16, and 17 past the length of EnumCharacterDressType. If we do find a night vision device in one of these slots
         * we can assume the vanilla slot isn't in use and change stack to reference the helmet we found.
         */
        int coHelmSlots = Enum.GetValues<EnumCharacterDressType>().Length + 15; // There has to be a better way to do this, the logic will break if CO reorders their armor indexes

        for (int i = 0; i < 3; i++)
        {
            ItemStack coStack = inv[coHelmSlots + i]?.Itemstack;
            if (coStack != null && coStack.Collectible is ItemNightvisiondevice)
                stack = coStack;
        }
        
        if (stack == null || stack.Collectible is not ItemNightvisiondevice) return false;

        var fuelLeft = Math.Max(0, stack.Attributes.GetDecimal("fuelHours"));
        if (fuelLeft <= 0) return false;

        if (Config.NightVision.HiglightRange > 0)
        {
            glowingEntities.Scan(api.World);
        }

        if (!Config.NightVision.OverlayEnabled) return true;

        IShaderProgram curShader = api.Render.CurrentActiveShader;
        curShader?.Stop();

        overlayShaderProg.Use();

        api.Render.GlToggleBlend(true, EnumBlendMode.Glow);

        overlayShaderProg.Uniform("time", api.World.ElapsedMilliseconds / 1000f);
        overlayShaderProg.Uniform("height", api.Render.FrameHeight);

        api.Render.RenderMesh(quadRef);
        overlayShaderProg.Stop();

        curShader?.Use();

        return true;
    }
}
