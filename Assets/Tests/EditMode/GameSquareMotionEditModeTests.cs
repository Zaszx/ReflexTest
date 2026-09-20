using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public sealed class GameSquareMotionEditModeTests
{
    private readonly List<GameObject> objects = new List<GameObject>();
    private NeonMotionSettings originalSettings;
    private NeonMotionSettings testSettings;
    private bool? originalReducedEffects;
    private static readonly FieldInfo SettingsField = typeof(NeonMotion).GetField("settings", BindingFlags.Static | BindingFlags.NonPublic);
    private static readonly FieldInfo ReducedEffectsField = typeof(NeonTheme).GetField("reduced", BindingFlags.Static | BindingFlags.NonPublic);

    [SetUp]
    public void SetUp()
    {
        // Isolate the cached preference without invoking its PlayerPrefs setter.
        originalReducedEffects = (bool?)ReducedEffectsField.GetValue(null);
        ReducedEffectsField.SetValue(null, (bool?)false);
        originalSettings = NeonMotion.T;
        testSettings = Object.Instantiate(originalSettings);
        testSettings.hideFlags = HideFlags.DontSave;
        SettingsField.SetValue(null, testSettings);
        NeonMotion.T.targetRoleDuration = .11f;
        NeonMotion.T.targetRoleRetargetDuration = .065f;
        NeonMotion.T.targetAppearDuration = .10f;
        NeonMotion.T.targetExitDuration = .09f;
        NeonMotion.T.targetRoleGap = .06f;
        NeonMotion.T.targetSpawnScale = .82f;
        NeonMotion.T.targetSpawnAlpha = .65f;
        NeonMotion.T.targetExitAlpha = .35f;
        NeonMotion.T.targetExitContraction = .08f;
        NeonMotion.T.correctCellDuration = .16f;
        NeonMotion.T.wrongCellDuration = .20f;
        NeonMotion.T.correctCellTint = .22f;
        NeonMotion.T.wrongCellTint = .32f;
        NeonMotion.T.reducedEffectsStrength = .45f;
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            foreach (GameObject item in objects) Object.DestroyImmediate(item);
            objects.Clear();
        }
        finally
        {
            ReducedEffectsField.SetValue(null, originalReducedEffects);
            SettingsField.SetValue(null, originalSettings);
            if (testSettings != null) Object.DestroyImmediate(testSettings);
        }
    }

    [Test]
    public void NormalRolesMorphConcurrentlyWithAnImmediatelyReusedConsumedCell()
    {
        GameSquare oldSmall = Create(GameSquare.LitSize.Small);
        GameSquare oldMedium = Create(GameSquare.LitSize.Medium);
        GameSquare consumed = Create(GameSquare.LitSize.Large);
        consumed.PlayConsumedFeedback();
        consumed.SetLitSize(GameSquare.LitSize.Small, true);
        oldSmall.SetLitSize(GameSquare.LitSize.Medium, true);
        oldMedium.SetLitSize(GameSquare.LitSize.Large, true);
        Assert.That(consumed.HasRetiringVisual, Is.True);
        Assert.That(consumed.currentLitSize, Is.EqualTo(GameSquare.LitSize.Small));
        Assert.That(oldSmall.currentLitSize, Is.EqualTo(GameSquare.LitSize.Medium));
        Assert.That(oldMedium.currentLitSize, Is.EqualTo(GameSquare.LitSize.Large));
        Assert.That(oldSmall.RenderedScale, Is.LessThan(.7f));
        Assert.That(oldMedium.RenderedScale, Is.LessThan(1f));
        Assert.That(oldSmall.IsTargetAnimating && oldMedium.IsTargetAnimating && consumed.IsTargetAnimating, Is.True);
        for (int i = 0; i < 15; i++)
        {
            AssertOrdered(consumed, oldSmall, oldMedium);
            consumed.AdvancePresentation(.01f);
            oldSmall.AdvancePresentation(.01f);
            oldMedium.AdvancePresentation(.01f);
        }
        Assert.That(consumed.HasRetiringVisual, Is.False);
        AssertSettled(consumed, .4f);
        AssertSettled(oldSmall, .7f);
        AssertSettled(oldMedium, 1f);
    }

    [Test]
    public void ReverseRolesShrinkWhileNewLargeAppearsAtFullSize()
    {
        GameSquare consumed = Create(GameSquare.LitSize.Small);
        GameSquare oldMedium = Create(GameSquare.LitSize.Medium);
        GameSquare oldLarge = Create(GameSquare.LitSize.Large);
        consumed.PlayConsumedFeedback();
        consumed.SetLitSize(GameSquare.LitSize.Large, true);
        oldMedium.SetLitSize(GameSquare.LitSize.Small, true);
        oldLarge.SetLitSize(GameSquare.LitSize.Medium, true);
        Assert.That(consumed.RenderedScale, Is.EqualTo(1f));
        Assert.That(consumed.RenderedAlpha, Is.GreaterThan(0f));
        Assert.That(oldMedium.RenderedScale, Is.GreaterThan(.4f));
        Assert.That(oldLarge.RenderedScale, Is.GreaterThan(.7f));
        for (int i = 0; i < 15; i++)
        {
            AssertOrdered(oldMedium, oldLarge, consumed);
            Assert.That(consumed.RenderedScale, Is.EqualTo(1f));
            consumed.AdvancePresentation(.01f);
            oldMedium.AdvancePresentation(.01f);
            oldLarge.AdvancePresentation(.01f);
        }
        AssertSettled(oldMedium, .4f);
        AssertSettled(oldLarge, .7f);
        AssertSettled(consumed, 1f);
        Assert.That(consumed.HasRetiringVisual, Is.False);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ThousandRapidAssignmentsStayAtLatestSnapshotWithBoundedVisuals(bool reverse)
    {
        GameSquare[] cells = { Create(GameSquare.LitSize.Small), Create(GameSquare.LitSize.Medium),
            Create(GameSquare.LitSize.Large), Create(GameSquare.LitSize.None) };
        GridSequenceState sequence = new GridSequenceState(0, 1, 2);
        DeterministicRandom random = new DeterministicRandom(71823);
        int initialObjects = CountChildren(cells);
        int reuseCount = 0;
        for (int tap = 0; tap < 1000; tap++)
        {
            int consumed = reverse ? sequence.small : sequence.large;
            bool accepted = reverse ? GridSequenceRules.AdvanceReverse(ref sequence, cells.Length, ref random)
                : GridSequenceRules.AdvanceNormal(ref sequence, cells.Length, ref random);
            Assert.That(accepted, Is.True);
            cells[consumed].PlayConsumedFeedback();
            if (consumed == (reverse ? sequence.large : sequence.small)) reuseCount++;
            AssignSnapshot(cells, sequence);
            AssertOrdered(cells[sequence.small], cells[sequence.medium], cells[sequence.large]);
            foreach (GameSquare cell in cells) cell.AdvancePresentation(.002f);
            AssertOrdered(cells[sequence.small], cells[sequence.medium], cells[sequence.large]);
        }
        foreach (GameSquare cell in cells) cell.AdvancePresentation(1f);
        Assert.That(reuseCount, Is.Zero, "The just-consumed cell must remain empty for this sequence advance.");
        Assert.That(CountChildren(cells), Is.EqualTo(initialObjects), "Exit effects must reuse their preallocated graphics.");
        AssertSettled(cells[sequence.small], .4f);
        AssertSettled(cells[sequence.medium], .7f);
        AssertSettled(cells[sequence.large], 1f);
        foreach (GameSquare cell in cells) Assert.That(cell.HasRetiringVisual, Is.False);
    }

    [TestCase(1f / 120f)]
    [TestCase(1f / 60f)]
    [TestCase(1f / 30f)]
    [TestCase(.1f)]
    public void RepresentativeFrameIntervalsPreserveBothRuleOrdersAndSettleExactly(float frameDelta)
    {
        foreach (bool reverse in new[] { false, true })
        {
            GameSquare[] cells = { Create(GameSquare.LitSize.Small), Create(GameSquare.LitSize.Medium),
                Create(GameSquare.LitSize.Large), Create(GameSquare.LitSize.None) };
            GridSequenceState sequence = new GridSequenceState(0, 1, 2);
            DeterministicRandom random = new DeterministicRandom(71823);
            int initialObjects = CountChildren(cells);
            for (int tap = 0; tap < 8; tap++)
            {
                int consumed = reverse ? sequence.small : sequence.large;
                bool accepted = reverse ? GridSequenceRules.AdvanceReverse(ref sequence, cells.Length, ref random)
                    : GridSequenceRules.AdvanceNormal(ref sequence, cells.Length, ref random);
                Assert.That(accepted, Is.True);
                cells[consumed].PlayConsumedFeedback();
                AssignSnapshot(cells, sequence);
                Assert.That(cells[sequence.small].currentLitSize, Is.EqualTo(GameSquare.LitSize.Small));
                Assert.That(cells[sequence.medium].currentLitSize, Is.EqualTo(GameSquare.LitSize.Medium));
                Assert.That(cells[sequence.large].currentLitSize, Is.EqualTo(GameSquare.LitSize.Large));
                AssertOrdered(cells[sequence.small], cells[sequence.medium], cells[sequence.large]);
                for (int step = 0; step < Mathf.CeilToInt(.25f / frameDelta); step++)
                {
                    foreach (GameSquare cell in cells) cell.AdvancePresentation(frameDelta);
                    AssertOrdered(cells[sequence.small], cells[sequence.medium], cells[sequence.large]);
                }
                AssertSettled(cells[sequence.small], .4f);
                AssertSettled(cells[sequence.medium], .7f);
                AssertSettled(cells[sequence.large], 1f);
                foreach (GameSquare cell in cells)
                {
                    Assert.That(cell.IsTargetAnimating, Is.False);
                    Assert.That(cell.HasRetiringVisual, Is.False);
                    if (cell.currentLitSize == GameSquare.LitSize.None)
                        Assert.That(cell.RenderedAlpha, Is.EqualTo(0f));
                }
            }
            Assert.That(CountChildren(cells), Is.EqualTo(initialObjects));
        }
    }

    [Test]
    public void ExternalPauseFreezesCurrentMorphRetiringVisualAndDamageCue()
    {
        GameSquare cell = Create(GameSquare.LitSize.Large);
        cell.PlayConsumedFeedback();
        cell.SetLitSize(GameSquare.LitSize.Small, true);
        cell.PlayTapFeedback(false);
        cell.AdvancePresentation(.02f);
        float scale = cell.RenderedScale;
        float alpha = cell.RenderedAlpha;
        float exitAlpha = cell.RetiringAlpha;
        Color color = cell.bgImage.color;
        cell.SetAnimationsPaused(true);
        cell.AdvancePresentation(5f);
        Assert.That(cell.RenderedScale, Is.EqualTo(scale));
        Assert.That(cell.RenderedAlpha, Is.EqualTo(alpha));
        Assert.That(cell.RetiringAlpha, Is.EqualTo(exitAlpha));
        Assert.That(cell.bgImage.color, Is.EqualTo(color));
        cell.SetAnimationsPaused(false);
        cell.AdvancePresentation(.25f);
        AssertSettled(cell, .4f);
        Assert.That(cell.HasRetiringVisual, Is.False);
    }

    [Test]
    public void RepeatedMistakeFeedbackLeavesRoleAndHitGeometryUntouched()
    {
        GameSquare cell = Create(GameSquare.LitSize.Medium);
        RectTransform root = (RectTransform)cell.transform;
        root.localScale = new Vector3(.81f, .81f, 1f);
        root.localRotation = Quaternion.Euler(0f, 0f, 43f);
        root.anchoredPosition = new Vector2(21f, -11f);
        Vector3 beforeScale = root.localScale;
        Quaternion beforeRotation = root.localRotation;
        Vector2 beforePosition = root.anchoredPosition;
        Color baseColor = cell.bgImage.color;
        for (int i = 0; i < 12; i++)
        {
            cell.PlayTapFeedback(false);
            cell.AdvancePresentation(.012f);
            Assert.That(cell.currentLitSize, Is.EqualTo(GameSquare.LitSize.Medium));
            Assert.That(cell.RenderedScale, Is.EqualTo(.7f));
            Assert.That(cell.RenderedAlpha, Is.EqualTo(1f));
            Assert.That(cell.HasRetiringVisual, Is.False);
        }
        cell.AdvancePresentation(.5f);
        Assert.That(cell.bgImage.color, Is.EqualTo(baseColor));
        Assert.That(root.localScale, Is.EqualTo(beforeScale));
        Assert.That(root.localRotation, Is.EqualTo(beforeRotation));
        Assert.That(root.anchoredPosition, Is.EqualTo(beforePosition));
        Assert.That(cell.bgImage.raycastTarget, Is.True);
        foreach (PrecisionCellFrame frame in cell.GetComponentsInChildren<PrecisionCellFrame>(true))
            Assert.That(frame.raycastTarget, Is.False);
    }

    [Test]
    public void RenderedOutlineCornersCaptureTheLiveTransformedTargetBeforeReuse()
    {
        GameSquare cell = Create(GameSquare.LitSize.Medium);
        RectTransform root = (RectTransform)cell.transform;
        root.anchoredPosition = new Vector2(21f, -11f);
        root.localRotation = Quaternion.Euler(0f, 0f, 43f);
        root.localScale = new Vector3(.81f, .81f, 1f);
        Vector3[] expected = new Vector3[4];
        Vector3[] captured = new Vector3[4];

        cell.outlineImage.rectTransform.GetWorldCorners(expected);
        Assert.That(cell.TryGetRenderedOutlineCorners(captured), Is.True);
        for (int i = 0; i < captured.Length; i++)
            Assert.That(Vector3.Distance(captured[i], expected[i]), Is.LessThan(.0001f));

        cell.PlayConsumedFeedback();
        cell.SetLitSize(GameSquare.LitSize.Large, true);
        Assert.That(cell.TryGetRenderedOutlineCorners(captured), Is.True,
            "The reused cell exposes its new rendered target, so callers must capture before consumption.");
        Assert.That(Vector3.Distance(captured[0], expected[0]), Is.GreaterThan(.0001f));
    }

    [Test]
    public void RenderedOutlineCornerCaptureRejectsMissingOrInvisibleTargets()
    {
        GameSquare cell = Create(GameSquare.LitSize.None);
        Assert.That(cell.TryGetRenderedOutlineCorners(null), Is.False);
        Assert.That(cell.TryGetRenderedOutlineCorners(new Vector3[3]), Is.False);
        Assert.That(cell.TryGetRenderedOutlineCorners(new Vector3[4]), Is.False);
    }

    [Test]
    public void CorrectFeedbackUsesAnIndependentSuccessLayerDuringImmediateCellReuse()
    {
        GameSquare cell = Create(GameSquare.LitSize.Large);
        PrecisionCellFrame target = cell.outlineImage.GetComponentInChildren<PrecisionCellFrame>();
        PrecisionCellFrame success = cell.bgImage.transform.Find("SuccessOutline").GetComponent<PrecisionCellFrame>();
        cell.PlayTapFeedback(true);

        Assert.That(success.gameObject.activeSelf, Is.True);
        Assert.That(success.raycastTarget, Is.False);
        Assert.That(success.CornerFraction, Is.LessThanOrEqualTo(.1f),
            "Success feedback uses bounded corner ticks, never a target-like hollow square.");
        Assert.That(target.color.a, Is.EqualTo(1f), "Success feedback must not consume the active target outline.");

        cell.PlayConsumedFeedback();
        cell.SetLitSize(GameSquare.LitSize.Small, true);
        Assert.That(success.gameObject.activeSelf, Is.True, "The outgoing acknowledgement owns its own layer.");
        Assert.That(cell.currentLitSize, Is.EqualTo(GameSquare.LitSize.Small));
        Assert.That(cell.outlineImage.gameObject.activeSelf, Is.True);
        cell.AdvancePresentation(1f);
        Assert.That(success.gameObject.activeSelf, Is.False);
    }

    [Test]
    public void PaletteReplacementCancelsBothTransientLocalFeedbackLayers()
    {
        GameSquare cell = Create(GameSquare.LitSize.Large);
        PrecisionCellFrame damage = cell.bgImage.transform.Find("DamageOutline").GetComponent<PrecisionCellFrame>();
        PrecisionCellFrame success = cell.bgImage.transform.Find("SuccessOutline").GetComponent<PrecisionCellFrame>();
        cell.PlayTapFeedback(false);
        Assert.That(damage.gameObject.activeSelf, Is.True);
        cell.SetBasePalette(new Color(.31f, .07f, .16f, 1f), new Color(1f, .25f, .12f, 1f));
        Assert.That(damage.gameObject.activeSelf, Is.False);

        cell.PlayTapFeedback(true);
        Assert.That(success.gameObject.activeSelf, Is.True);
        cell.SetBasePalette(new Color(.08f, .11f, .19f, 1f), new Color(.1f, .9f, .8f, 1f));
        Assert.That(success.gameObject.activeSelf, Is.False);
    }

    [Test]
    public void TerminalFreezePreservesCurrentRolePoseWhileItsDamageLayerCanSettle()
    {
        GameSquare cell = Create(GameSquare.LitSize.Large);
        cell.SetLitSize(GameSquare.LitSize.Small, true);
        cell.AdvancePresentation(.02f);
        float scale = cell.RenderedScale;
        float alpha = cell.RenderedAlpha;
        cell.PlayDamageFeedback();
        cell.BeginTerminalPresentation();
        cell.SetTerminalShutdown(.5f);
        cell.AdvancePresentation(.3f);
        Assert.That(cell.RenderedScale, Is.EqualTo(scale));
        Assert.That(cell.RenderedAlpha, Is.EqualTo(alpha));
        PrecisionCellFrame damage = cell.bgImage.transform.Find("DamageOutline").GetComponent<PrecisionCellFrame>();
        Assert.That(damage.raycastTarget, Is.False);
        Assert.That(damage.gameObject.activeSelf, Is.False, "The finite localized damage layer settles during terminal shutdown.");
    }

    [Test]
    public void TerminalShutdownFadesButDoesNotRemoveAnAcceptedRetiringOutline()
    {
        GameSquare cell = Create(GameSquare.LitSize.Large);
        cell.PlayConsumedFeedback();
        cell.AdvancePresentation(.02f);
        PrecisionCellFrame retiring = cell.bgImage.transform.Find("RetiringTarget").GetComponent<PrecisionCellFrame>();
        float alpha = retiring.color.a;
        Vector3 scale = retiring.rectTransform.localScale;
        cell.BeginTerminalPresentation();
        cell.SetTerminalShutdown(.5f);
        Assert.That(cell.HasRetiringVisual, Is.True);
        Assert.That(retiring.rectTransform.localScale, Is.EqualTo(scale));
        Assert.That(retiring.color.a, Is.EqualTo(alpha * .5f).Within(.0001f));
    }

    [Test]
    public void DisableCallbackNormalizesLatestRoleAndCannotReplayObsoleteExitOnReenable()
    {
        GameSquare cell = Create(GameSquare.LitSize.Small);
        Color baseColor = cell.bgImage.color;
        cell.PlayConsumedFeedback();
        cell.SetLitSize(GameSquare.LitSize.Large, true);
        cell.PlayTapFeedback(true);
        cell.AdvancePresentation(.025f);
        cell.gameObject.SetActive(false);
        // Non-ExecuteAlways behaviours do not receive Play Mode lifecycle
        // callbacks in this Edit Mode fixture; exercise the boundary explicitly.
        // Runtime QA separately verifies Unity's actual disable/enable dispatch.
        typeof(GameSquare).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(cell, null);
        AssertSettled(cell, 1f);
        Assert.That(cell.HasRetiringVisual, Is.False);
        Assert.That(cell.bgImage.color, Is.EqualTo(baseColor));
        cell.gameObject.SetActive(true);
        cell.AdvancePresentation(1f);
        AssertSettled(cell, 1f);
        Assert.That(cell.outlineImage.gameObject.activeSelf, Is.True);
    }

    [Test]
    public void NonAnimatedRestoreCancelsRetiringEffectAndReconstructsExactRole()
    {
        GameSquare cell = Create(GameSquare.LitSize.Large);
        cell.PlayConsumedFeedback();
        cell.SetLitSize(GameSquare.LitSize.Small, true);
        cell.AdvancePresentation(.015f);
        cell.SetLitSize(GameSquare.LitSize.Medium, false);
        AssertSettled(cell, .7f);
        Assert.That(cell.HasRetiringVisual, Is.False);
        cell.AdvancePresentation(1f);
        AssertSettled(cell, .7f);
    }

    [Test]
    public void ZeroDurationsSettleWithoutPendingVisualWork()
    {
        NeonMotion.T.targetRoleDuration = 0f;
        NeonMotion.T.targetRoleRetargetDuration = 0f;
        NeonMotion.T.targetAppearDuration = 0f;
        NeonMotion.T.targetExitDuration = 0f;
        NeonMotion.T.correctCellDuration = 0f;
        NeonMotion.T.wrongCellDuration = 0f;
        GameSquare cell = Create(GameSquare.LitSize.Large);
        Color baseColor = cell.bgImage.color;
        cell.PlayConsumedFeedback();
        cell.SetLitSize(GameSquare.LitSize.Small, true);
        cell.PlayTapFeedback(false);
        AssertSettled(cell, .4f);
        Assert.That(cell.HasRetiringVisual, Is.False);
        Assert.That(cell.bgImage.color, Is.EqualTo(baseColor));
        cell.SetLitSize(GameSquare.LitSize.Medium, true);
        AssertSettled(cell, .7f);
        cell.SetLitSize(GameSquare.LitSize.None, true);
        Assert.That(cell.RenderedAlpha, Is.EqualTo(0f));
        Assert.That(cell.outlineImage.gameObject.activeSelf, Is.False);
    }

    [Test]
    public void GridRenderingUsesSupportedCoverageMaterialAndRequestsUv1()
    {
        Canvas canvas; GameSquare cell = CreateCanvasCell(out canvas);
        Material material = NeonGridRendering.Material;
        Assert.That(material, Is.Not.Null, "The activated grid coverage material must be loadable and supported.");
        Assert.That(material.shader, Is.Not.Null);
        Assert.That(material.shader.isSupported, Is.True);
        Assert.That(cell.bgImage.GetComponent<NeonGridFill>(), Is.Not.Null);
        Assert.That(cell.bgImage.material, Is.SameAs(material));
        Assert.That((canvas.additionalShaderChannels & AdditionalCanvasShaderChannels.TexCoord1) != 0,
            Is.True, "Analytic coverage requires the per-vertex UV1 shape payload.");
    }

    [Test]
    public void PaddedGridGeometryKeepsTransformedHitRectAndRaycastBounds()
    {
        Canvas canvas; GameSquare cell = CreateCanvasCell(out canvas);
        RectTransform rect = (RectTransform)cell.transform;
        rect.sizeDelta = new Vector2(64f, 64f);
        rect.anchoredPosition = new Vector2(27f, -19f);
        rect.localRotation = Quaternion.Euler(0f, 0f, 37f);
        rect.localScale = new Vector3(.73f, .73f, 1f);
        Rect authoredRect = rect.rect;
        Canvas.ForceUpdateCanvases();
        Vector2 center = RectTransformUtility.WorldToScreenPoint(null, rect.position);
        cell.bgImage.SetVerticesDirty(); Canvas.ForceUpdateCanvases();
        Mesh generated = cell.bgImage.canvasRenderer.GetMesh();
        Assert.That(rect.rect, Is.EqualTo(authoredRect));
        Assert.That(generated.bounds.extents.x, Is.GreaterThan(rect.rect.width * .5f + 8f),
            "Coverage geometry should include transparent raster padding beyond the authored cell rect.");
        Assert.That(generated.bounds.extents.y, Is.GreaterThan(rect.rect.height * .5f + 8f));
        Assert.That(RectTransformUtility.RectangleContainsScreenPoint(rect, center), Is.True);
        Vector2 paddedOutside = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(new Vector3(rect.rect.xMax + 4f, 0f, 0f)));
        Assert.That(RectTransformUtility.RectangleContainsScreenPoint(rect, paddedOutside), Is.False,
            "Mesh padding must not enlarge the RectTransform hit bounds.");
        Assert.That(cell.bgImage.raycastTarget, Is.True);
        Assert.That(cell.bgImage.Raycast(center, null), Is.True,
            "The transformed cell center must remain raycastable after analytic mesh padding.");
        // GraphicRaycaster checks rectangle containment before Graphic.Raycast;
        // the latter tests filters only. Actual rendered hit dispatch is in QA.
    }

    [TestCase(.4f, .7f, 1f)]
    [TestCase(.05f, .9f, 1f)]
    [TestCase(.2f, .45f, .88f)]
    public void ConfiguredBandsRemainStrictlyOrderedAcrossAllRetargetValues(float small, float medium, float large)
    {
        float upperSmall = TargetRoleBands.ClampStart(GameSquare.LitSize.Small, 10f, small, medium, large, .06f);
        float lowerMedium = TargetRoleBands.ClampStart(GameSquare.LitSize.Medium, -10f, small, medium, large, .06f);
        float upperMedium = TargetRoleBands.ClampStart(GameSquare.LitSize.Medium, 10f, small, medium, large, .06f);
        float lowerLarge = TargetRoleBands.ClampStart(GameSquare.LitSize.Large, -10f, small, medium, large, .06f);
        Assert.That(upperSmall, Is.LessThan(lowerMedium));
        Assert.That(upperMedium, Is.LessThan(lowerLarge));
        Assert.That(upperSmall, Is.GreaterThanOrEqualTo(small));
        Assert.That(lowerMedium, Is.LessThanOrEqualTo(medium));
        Assert.That(upperMedium, Is.GreaterThanOrEqualTo(medium));
        Assert.That(lowerLarge, Is.LessThanOrEqualTo(large));
    }

    [Test]
    public void ExternalLevelPresentationRetargetsWithoutAdvanceAndPreservesHitRect()
    {
        GameSquare cell = Create(GameSquare.LitSize.Small);
        RectTransform root = (RectTransform)cell.transform;
        root.sizeDelta = new Vector2(137f, 121f); root.anchoredPosition = new Vector2(19f, -13f);
        Vector2 sizeBefore = root.rect.size; Vector2 positionBefore = root.anchoredPosition; Quaternion rotationBefore = root.localRotation;
        cell.BeginLevelPresentation(GameSquare.LitSize.Large, .3f, .6f, .9f);
        Assert.That(cell.currentLitSize, Is.EqualTo(GameSquare.LitSize.Large));
        Assert.That(cell.transform.Find("RetiringTarget").gameObject.activeSelf, Is.True);
        float start = cell.RenderedScale;
        cell.AdvancePresentation(10f);
        Assert.That(cell.RenderedScale, Is.EqualTo(start), "Externally owned presentation must remain paused during ordinary stepping.");
        cell.RenderLevelPresentation(0f);
        Assert.That(cell.RenderedAlpha, Is.EqualTo(0f));
        cell.RenderLevelPresentation(.5f);
        Assert.That(cell.RenderedAlpha, Is.Zero, "Incoming targets wait until the grid morph phase is complete.");
        Assert.That(cell.transform.Find("RetiringTarget").GetComponent<PrecisionCellFrame>().color.a, Is.Zero.Within(.0001f));
        cell.RenderLevelPresentation(.86f);
        float incoming = NeonMotion.Ease(.5f);
        Assert.That(cell.RenderedScale, Is.EqualTo(.9f * incoming).Within(.0001f)); Assert.That(cell.RenderedAlpha, Is.EqualTo(incoming).Within(.0001f));
        cell.RenderLevelPresentation(1f);
        Assert.That(cell.RenderedAlpha, Is.EqualTo(1f));
        Assert.That(root.rect.size, Is.EqualTo(sizeBefore)); Assert.That(root.anchoredPosition, Is.EqualTo(positionBefore)); Assert.That(root.localRotation, Is.EqualTo(rotationBefore));
        cell.EndLevelPresentation();
        AssertSettled(cell, .9f); Assert.That(cell.HasRetiringVisual, Is.False);
    }

    [Test]
    public void LevelPresentationPaletteReplacementDoesNotRetainFeedbackTint()
    {
        GameSquare cell = Create(GameSquare.LitSize.Large);
        Color sourceFill = new Color(.08f, .11f, .19f, 1f), sourceOutline = new Color(.1f, .9f, .8f, 1f);
        Color destinationFill = new Color(.31f, .07f, .16f, 1f), destinationOutline = new Color(1f, .25f, .12f, 1f);
        cell.SetBasePalette(sourceFill, sourceOutline); cell.PlayTapFeedback(true); cell.AdvancePresentation(.03f);
        cell.SetBasePalette(destinationFill, destinationOutline);
        Assert.That(cell.bgImage.color, Is.EqualTo(destinationFill));
        cell.BeginLevelPresentation(GameSquare.LitSize.Large, .4f, .7f, 1f);
        cell.SetBasePalette(destinationFill, destinationOutline); cell.RenderLevelPresentation(1f); cell.EndLevelPresentation();
        cell.SetAnimationsPaused(false);
        cell.PlayTapFeedback(true); cell.AdvancePresentation(1f);
        Assert.That(cell.bgImage.color, Is.EqualTo(destinationFill), "Correct feedback must decay to the newly supplied base palette.");
    }

    [Test]
    public void RankLadderSupportsTwoThroughFiveStrictlyIncreasingOutlineSizes()
    {
        GameSquare cell = Create(GameSquare.LitSize.Large);
        for (int count = 2; count <= 5; count++)
        {
            float previous = 0f;
            for (int rank = 0; rank < count; rank++)
            {
                cell.SetTargetRank(rank, count, .4f, .7f, 1f, false);
                Assert.That(cell.CurrentTargetRank, Is.EqualTo(rank));
                Assert.That(cell.RenderedScale, Is.GreaterThan(previous));
                previous = cell.RenderedScale;
            }
        }
    }

    [Test]
    public void NormalAndReverseConsumedFeedbackUseIndependentRetiringLayers()
    {
        GameSquare cell = Create(GameSquare.LitSize.Large);
        cell.PlayConsumedFeedback();
        cell.SetTargetRank(0, 5, .4f, .7f, 1f, true);
        cell.AdvancePresentation(.02f);
        float normalScale = cell.transform.Find("RetiringTarget").localScale.x;
        cell.PlayConsumedFeedback(reverse: true);
        cell.SetTargetRank(4, 5, .4f, .7f, 1f, true);
        cell.AdvancePresentation(.02f);
        Assert.That(cell.GetComponentsInChildren<PrecisionCellFrame>(true).Length, Is.GreaterThanOrEqualTo(5), "A reused cell keeps a separate retiring outline layer.");
        Assert.That(normalScale, Is.GreaterThan(1f), "Normal consumption expands before it dissolves.");
    }

    private GameSquare Create(GameSquare.LitSize size)
    {
        GameObject root = new GameObject("MotionTestCell", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(GameSquare));
        objects.Add(root);
        ((RectTransform)root.transform).sizeDelta = new Vector2(200f, 200f);
        GameSquare cell = root.GetComponent<GameSquare>();
        cell.bgImage = root.GetComponent<Image>();
        GameObject outline = new GameObject("Outline", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        outline.transform.SetParent(root.transform, false);
        cell.outlineImage = outline.GetComponent<Image>();
        RectTransform rect = cell.outlineImage.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        cell.Setup(0, 0, null, null, NeonTheme.T.Surface, NeonTheme.T.Target, .4f, .7f, 1f);
        cell.SetLitSize(size, false);
        return cell;
    }

    private GameSquare CreateCanvasCell(out Canvas canvas)
    {
        GameObject canvasObject = new GameObject("Grid rendering test canvas", typeof(RectTransform), typeof(Canvas));
        objects.Add(canvasObject); canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        GameObject root = new GameObject("Grid rendering test cell", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(GameSquare));
        objects.Add(root); root.transform.SetParent(canvas.transform, false);
        ((RectTransform)root.transform).sizeDelta = new Vector2(200f, 200f);
        GameSquare cell = root.GetComponent<GameSquare>(); cell.bgImage = root.GetComponent<Image>();
        GameObject outline = new GameObject("Outline", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        objects.Add(outline); outline.transform.SetParent(root.transform, false); cell.outlineImage = outline.GetComponent<Image>();
        RectTransform outlineRect = cell.outlineImage.rectTransform; outlineRect.anchorMin = Vector2.zero; outlineRect.anchorMax = Vector2.one; outlineRect.offsetMin = outlineRect.offsetMax = Vector2.zero;
        cell.Setup(0, 0, null, null, NeonTheme.T.Surface, NeonTheme.T.Target, .4f, .7f, 1f);
        cell.SetLitSize(GameSquare.LitSize.Large, false);
        return cell;
    }

    private static void AssignSnapshot(GameSquare[] cells, GridSequenceState sequence)
    {
        for (int i = 0; i < cells.Length; i++)
            cells[i].SetLitSize(i == sequence.small ? GameSquare.LitSize.Small : i == sequence.medium ? GameSquare.LitSize.Medium
                : i == sequence.large ? GameSquare.LitSize.Large : GameSquare.LitSize.None, true);
    }

    private static void AssertOrdered(GameSquare small, GameSquare medium, GameSquare large)
    {
        Assert.That(small.RenderedScale, Is.LessThan(medium.RenderedScale));
        Assert.That(medium.RenderedScale, Is.LessThan(large.RenderedScale));
        Assert.That(small.RenderedAlpha, Is.GreaterThan(0f));
        Assert.That(medium.RenderedAlpha, Is.GreaterThan(0f));
        Assert.That(large.RenderedAlpha, Is.GreaterThan(0f));
    }

    private static void AssertSettled(GameSquare cell, float scale)
    {
        Assert.That(cell.IsTargetAnimating, Is.False);
        Assert.That(cell.RenderedScale, Is.EqualTo(scale).Within(.00001f));
        Assert.That(cell.RenderedAlpha, Is.EqualTo(1f));
    }

    private static int CountChildren(GameSquare[] cells)
    {
        int count = 0;
        foreach (GameSquare cell in cells) count += cell.GetComponentsInChildren<Transform>(true).Length;
        return count;
    }
}
