

// using System.Diagnostics.CodeAnalysis;
// using System.Numerics;
// using AutomationFramework;
// using AutomationFramework.Extensions;
// using AutomationRunner.Scripting;
// using AutomationRunner.Services;
// using Microsoft.Extensions.Logging;
// using OpenCvSharp;

// namespace AutomationRunner.Scripts;

// public sealed class VisionShadesCrafting : BaseScript
// {
//     public VisionShadesCrafting(
//         AutomationFramework.Cursor cursor,
//         AutomationFramework.Keyboard keyboard,
//         IAutomationVisionFactory visionFactory,
//         ILogger<VisionShadesCrafting> logger)
//         : base(logger)
//     {
//         _cursor = cursor;
//         _keyboard = keyboard;
//         _visionFactory = visionFactory;
//     }

//     public override string Name => "vision-shades-crafting";

//     public override string Description => "Uses vision to find the correct buttons for wrist crafting and clicks them.";

//     private readonly AutomationFramework.Cursor _cursor;
//     private readonly AutomationFramework.Keyboard _keyboard;
//     private readonly IAutomationVisionFactory _visionFactory;
//     AutomationFramework.Vision _vision = null!;

//     private Dictionary<string, VisionTemplateLease> _templates = new Dictionary<string, VisionTemplateLease>();


//     protected override Task InitializeAsync(CancellationToken cancellationToken)
//     {
//         _vision = _visionFactory.Create();

//         // Load templates
//         AcquireTemplates
//         (
//             VisionTemplateFileNames.AB_ENGINEERING_BTN,
//             VisionTemplateFileNames.UI_TSM_ENGINEERING_WINDOW,
//             VisionTemplateFileNames.UI_TSM_MAIL_WINDOW,
//             VisionTemplateFileNames.UI_TSM_MAX_BTN,
//             VisionTemplateFileNames.UI_TSM_CRAFT_BTN,
//             VisionTemplateFileNames.UI_TSM_CLOSE_BTN,
//             VisionTemplateFileNames.AB_GILDED_TRADERS_BRUTOSAUR_BTN,
//             VisionTemplateFileNames.AB_TARGET_MAIL_NPC_BTN,
//             VisionTemplateFileNames.UI_TSM_MAILBOX_GROUPS_BTN,
//             VisionTemplateFileNames.UI_TSM_MAIL_SELECTED_GROUPS_BTN
//         );

//         return Task.CompletedTask;
//     }

//     public override void Dispose()
//     {   
//         if (_vision is null)
//         {
//             return;
//         }

//         // Release templates
//         foreach (var templateLease in _templates.Values)
//         {
//             templateLease.Dispose();
//         }
//         _templates.Clear();

//         // Dispose vision
//         _vision.Dispose();
//     }

//     protected override async Task RunAsync(CancellationToken cancellationToken)
//     {
//         var searchRegion = Screen.PrimaryScreen?.Bounds;

//         while (cancellationToken.IsCancellationRequested == false)
//         {
//             var engineeringWindowResult = await OpenEngineeringWindowAsync(searchRegion!.Value, cancellationToken: cancellationToken);

//             if (engineeringWindowResult.Success is false)
//             {
//                 break;
//             }

//             var engineeringWindowBounds = engineeringWindowResult.Match.ToGlobalBounds();

//             // Click TSM max button
//             var maxButtonImageMatch = await _vision.FindImageAsync
//             (
//                 _templates[VisionTemplateFileNames.UI_TSM_MAX_BTN].TemplateMat,
//                 0.7, 
//                 cancellationToken: cancellationToken, 
//                 searchRegion: engineeringWindowBounds
//             );

//             if (maxButtonImageMatch == null)
//             {
//                 _logger.LogWarning("Max button not found.");
//                 return;
//             }

//             var maxButtonBounds = maxButtonImageMatch.ToGlobalBounds();
//             maxButtonBounds = maxButtonBounds.Scale(1, 0.75f).Translate(-3 * maxButtonBounds.Width, 0);

//             var quantityInputRandomPoint = maxButtonBounds.GetRandomPointInBounds();
//             await _cursor.MoveToAsync(quantityInputRandomPoint, cancellationToken: cancellationToken);
//             await _cursor.ClickAsync(cancellationToken: cancellationToken);

//             // input the quantity to craft, which is 200 in this case
//             await _keyboard.TypeTextAsync("200", cancellationToken: cancellationToken);

//             // Click TSM craft button
//             var craftButtonImageMatch = await _vision.FindImageAsync(_templates[VisionTemplateFileNames.UI_TSM_CRAFT_BTN].TemplateMat, 0.8, cancellationToken: cancellationToken, searchRegion: engineeringWindowBounds);
//             if (craftButtonImageMatch == null)
//             {
//                 _logger.LogWarning("Craft button not found.");
//                 return;
//             }

//             var tsmCraftButtonRandomPoint = craftButtonImageMatch.ToGlobalBounds().Padd(30, 5).GetRandomPointInBounds();
//             await _cursor.MoveToAsync(tsmCraftButtonRandomPoint, cancellationToken: cancellationToken);
//             await _cursor.ClickAsync(cancellationToken: cancellationToken);


//             // wait for crafts to be complete about 100 sec
//             await Task.Delay(TimeSpan.FromSeconds(150).ApplyRandomFactor(0.9, 1.1), cancellationToken);

//             // Click TSM close button
//             var tsmCloseButtonImageMatch = await _vision.FindImageAsync(_templates[VisionTemplateFileNames.UI_TSM_CLOSE_BTN].TemplateMat, 0.70, cancellationToken: cancellationToken, searchRegion: searchRegion);
//             if (tsmCloseButtonImageMatch == null)
//             {
//                 _logger.LogWarning("TSM close button not found.");
//                 return;
//             }


//             var tsmCloseButtonCenter = tsmCloseButtonImageMatch.ToGlobalBounds().Center();
//             await _cursor.MoveToAsync(tsmCloseButtonCenter, cancellationToken: cancellationToken);
//             await Task.Delay(TimeSpan.FromSeconds(1).ApplyRandomFactor(), cancellationToken: cancellationToken);
//             await _cursor.ClickAsync(cancellationToken: cancellationToken);

//             // wait just a second for a possible last craft to complete
//             await Task.Delay(TimeSpan.FromSeconds(2).ApplyRandomFactor(), cancellationToken);

//             var mountRes = await MountBrutosaurAsync(searchRegion!.Value);
//             if (mountRes is false)
//             {
//                 _logger.LogInformation("Failed to mount the brutosaur.");
//                 return;
//             }

//             var mailWindowRes = await OpenMailWindowAsync(searchRegion!.Value, cancellationToken: cancellationToken);
//             if (mailWindowRes.Success is false)
//             {
//                 break;
//             }

//             var mailWindowBounds = mailWindowRes.Match.ToGlobalBounds();

//             // click the TSM mailbox groups button
//             var tsmMailboxGroupsImageMatch = await _vision.FindImageAsync(_templates[VisionTemplateFileNames.UI_TSM_MAILBOX_GROUPS_BTN].TemplateMat, 0.7, cancellationToken: cancellationToken, searchRegion: mailWindowBounds);
//             if (tsmMailboxGroupsImageMatch == null)
//             {
//                 _logger.LogWarning("Groups button not found.");
//                 return;
//             }

//             var tsmMailboxGroupsButtonRandomPoint = tsmMailboxGroupsImageMatch.ToGlobalBounds().Inset(10).GetRandomPointInBounds();
//             await _cursor.MoveToAsync(tsmMailboxGroupsButtonRandomPoint, cancellationToken: cancellationToken);
//             await Task.Delay(TimeSpan.FromSeconds(1).ApplyRandomFactor(), cancellationToken);
//             await _cursor.ClickAsync();

//             //wait 1 sec, to ensure tab switch
//             await Task.Delay(TimeSpan.FromSeconds(2).ApplyRandomFactor());

//             // click the TSM mail selected groups button
//             var tsmMailSelectedGroupsImageMatch = await _vision.FindImageAsync(_templates[VisionTemplateFileNames.UI_TSM_MAIL_SELECTED_GROUPS_BTN].TemplateMat, 0.50, cancellationToken: cancellationToken, searchRegion: mailWindowBounds);
//             if (tsmMailSelectedGroupsImageMatch == null)
//             {
//                 _logger.LogWarning("Mail Selected Groups button not found.");
//                 return;
//             }

//             var tsmMailSelectedGroupsButtonRandomPoint = tsmMailSelectedGroupsImageMatch.ToGlobalBounds().Scale(2, 1).GetRandomPointInBounds();
//             await _cursor.MoveToAsync(tsmMailSelectedGroupsButtonRandomPoint, cancellationToken: cancellationToken);
//             await Task.Delay(TimeSpan.FromSeconds(1).ApplyRandomFactor(), cancellationToken);
//             await _cursor.ClickAsync(cancellationToken: cancellationToken);

//             // wait for all mail to send
//             await Task.Delay(TimeSpan.FromSeconds(20).ApplyRandomFactor(0.8, 1.2), cancellationToken);
//         }
//     }   

//     private void AcquireTemplates(params string[] filenames)
//     {
//         ArgumentNullException.ThrowIfNull(filenames);

//         foreach (var filename in filenames)
//         {
//             _templates[filename] = _vision.AcquireTemplateLease(filename);
//         }
//     }


//     public sealed class OpenWindowResult
//     {
//         public ImageMatchResult? Match { get; init; } // replace object with the real FindImageAsync match type

//         [MemberNotNullWhen(true, nameof(Match))]
//         public bool Success => Match is not null;
//     }
//     private async Task<OpenWindowResult> OpenEngineeringWindowAsync(Rectangle searchRegion, int maxAttempts = 3, CancellationToken cancellationToken = default)
//     {
//         ImageMatchResult? engineeringWindowMatch = null;

//         for (int attempt = 1; attempt <= maxAttempts; ++attempt)
//         {
//             // move mouse to center-ish with a random offset between -100 to 100
//             var newPos = searchRegion.Center() + new Vector2(Random.Shared.Next(-100, 100), Random.Shared.Next(-100, 100));
//             await _cursor.MoveToAsync(newPos);

//             //press "R" to clear cursor
//             await _keyboard.PressKeyAsync(VirtualKey.R, cancellationToken: cancellationToken);

//             // find the engineering AB button
//             var engineeringButtonImageMatch = await _vision.FindImageAsync(_templates[VisionTemplateFileNames.AB_ENGINEERING_BTN].TemplateMat, 0.7, cancellationToken: cancellationToken, searchRegion: searchRegion);
//             if (engineeringButtonImageMatch is null)
//             {
//                 _logger.LogWarning("Engineering button not found.");

//                 continue;
//             }

//             //move mouse over engineering button
//             var engineeringButtonRandomPoint = engineeringButtonImageMatch.ToGlobalBounds().Inset(20).GetRandomPointInBounds();
//             await _cursor.MoveToAsync(engineeringButtonRandomPoint, cancellationToken: cancellationToken);

//             // click it
//             await Task.Delay(TimeSpan.FromSeconds(1).ApplyRandomFactor());
//             await _cursor.ClickAsync(cancellationToken: cancellationToken);

//             // Wait for window to open
//             await Task.Delay(TimeSpan.FromSeconds(Random.Shared.NextFloat(5, 8)), cancellationToken);


//             // Check if the window is open and detected
//             engineeringWindowMatch = await _vision.FindImageAsync(_templates[VisionTemplateFileNames.UI_TSM_ENGINEERING_WINDOW].TemplateMat, 0.5, searchRegion, cancellationToken: cancellationToken);

//             if (engineeringWindowMatch is not null)
//             {
//                 break;
//             }
//         }

        
//         return new OpenWindowResult()
//         {
//             Match = engineeringWindowMatch
//         };
//     }

//     private async Task<bool> MountBrutosaurAsync(Rectangle searchRegion, int maxAttempts = 3, CancellationToken cancellationToken = default)
//     {
//         // buffs are always in the top half of the screen
//         var buffSearchRegion = new Rectangle(searchRegion.X, searchRegion.Y, searchRegion.Width, (int)(searchRegion.Height / 4f));

//         // action bars are always in the bottom half of the screen
//         var abSearchRegion = new Rectangle(searchRegion.X, (int)(searchRegion.Y + searchRegion.Height * 0.75f), searchRegion.Width, (int)(searchRegion.Height / 4f));

//         bool success = false;

//         for (int attempt = 1; attempt <= maxAttempts; ++attempt)
//         {
//             // move mouse to center-ish with a random offset between -100 to 100
//             var newPos = searchRegion.Center() + new Vector2(Random.Shared.Next(-100, 100), Random.Shared.Next(-100, 100));
//             await _cursor.MoveToAsync(newPos);

//             //clear cursor
//             await _keyboard.PressKeyAsync(VirtualKey.R, cancellationToken: cancellationToken);

//             //find the gilded brutosaur on the action bar
//             var mountAbImageMatch = await _vision.FindImageAsync(_templates[VisionTemplateFileNames.AB_GILDED_TRADERS_BRUTOSAUR_BTN].TemplateMat, 0.60, cancellationToken: cancellationToken, searchRegion: abSearchRegion);
//             if (mountAbImageMatch is null)
//             {
//                 _logger.LogWarning("GildedTradersBrutosaur not found.");
//                 continue;
//             }

//             var gildedTradersBrutosaurRandomPoint = mountAbImageMatch.ToGlobalBounds().Inset(20).GetRandomPointInBounds();
//             await _cursor.MoveToAsync(gildedTradersBrutosaurRandomPoint, cancellationToken: cancellationToken);
//             await Task.Delay(TimeSpan.FromSeconds(1).ApplyRandomFactor(), cancellationToken: cancellationToken);
//             await _cursor.ClickAsync(cancellationToken: cancellationToken);

//             // wait for mount to be summoned
//             await Task.Delay(TimeSpan.FromSeconds(3).ApplyRandomFactor(0.8, 1.2), cancellationToken);

//             //assert the mount is found in the buff list
//             var mountBuffImageMatch = await _vision.FindImageAsync(_templates[VisionTemplateFileNames.AB_GILDED_TRADERS_BRUTOSAUR_BTN].TemplateMat, 0.60, cancellationToken: cancellationToken, searchRegion: buffSearchRegion);
            
//             if (mountBuffImageMatch is not null)
//             {
//                 // the buff is found, therefore we are mounted on the brutosaur
//                 success = true;
//                 break;
//             }
//         }


//         return success;
//     }


//     private async Task<OpenWindowResult> OpenMailWindowAsync(Rectangle searchRegion, int maxAttempts = 3, CancellationToken cancellationToken = default)
//     {
//         ImageMatchResult? mailWindowMatch = null;

//         for (int attempt = 1; attempt <= maxAttempts; ++attempt)
//         {
//             // move mouse to center-ish with a random offset between -100 to 100
//             var newPos = searchRegion.Center() + new Vector2(Random.Shared.Next(-100, 100), Random.Shared.Next(-100, 100));
//             await _cursor.MoveToAsync(newPos);

//             //clear cursor
//             await _keyboard.PressKeyAsync(VirtualKey.R, cancellationToken: cancellationToken);


//             // find the interact with mail npc AB button
//             var targetMailButtonImageMatch = await _vision.FindImageAsync(_templates[VisionTemplateFileNames.AB_TARGET_MAIL_NPC_BTN].TemplateMat, 0.7, cancellationToken: cancellationToken, searchRegion: searchRegion);
//             if (targetMailButtonImageMatch is null)
//             {
//                 _logger.LogWarning("TargetMailButton not found.");

//                 continue;
//             }

//             //move mouse over engineering button
//             var targetMailButtonRandomPoint = targetMailButtonImageMatch.ToGlobalBounds().Inset(20).GetRandomPointInBounds();
//             await _cursor.MoveToAsync(targetMailButtonRandomPoint, cancellationToken: cancellationToken);

//             // click it
//             await Task.Delay(TimeSpan.FromSeconds(1).ApplyRandomFactor());
//             await _cursor.ClickAsync(cancellationToken: cancellationToken);
            
            
//             // interact with the mail NPC (with the interact keybind)
//             await Task.Delay(TimeSpan.FromSeconds(1).ApplyRandomFactor());
//             await _keyboard.PressKeyAsync(VirtualKey.F, cancellationToken: cancellationToken);

//             // Wait for window to open
//             await Task.Delay(TimeSpan.FromSeconds(1).ApplyRandomFactor(), cancellationToken);


//             // Check if the window is open and detected
//             mailWindowMatch = await _vision.FindImageAsync(_templates[VisionTemplateFileNames.UI_TSM_MAIL_WINDOW].TemplateMat, 0.5, searchRegion, cancellationToken: cancellationToken);

//             if (mailWindowMatch is not null)
//             {
//                 break;
//             }
//         }
        
//         return new OpenWindowResult()
//         {
//             Match = mailWindowMatch
//         };
//     }
// }