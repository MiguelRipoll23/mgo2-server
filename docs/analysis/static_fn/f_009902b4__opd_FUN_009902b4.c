/* containing function of static patch target(s); entry .opd.FUN_009902b4 @ 009902b4 */

void _opd_FUN_009902b4(int param_1)

{
  undefined4 uVar1;
  undefined4 *puVar2;
  undefined *puVar3;
  int iVar4;
  undefined4 uVar5;
  
  puVar3 = PTR_PTR_0121baf4;
  _opd_FUN_0097c124();
  iVar4 = _opd_FUN_0097c124();
  uVar1 = *(undefined4 *)(iVar4 + 0x2a4);
  uVar5 = _opd_FUN_009745d0(*(undefined4 *)(puVar3 + -0x7ffc));
  _opd_FUN_00a0ca3c(uVar1,0xf283c1,uVar5);
  _opd_FUN_00aa960c(param_1 + 0x6c);
  _opd_FUN_00aa960c(param_1 + 0xc1a58);
  _opd_FUN_00a2f1c8(param_1 + 0x183444);
  _opd_FUN_00a88ed8();
  _opd_FUN_00a89224();
  _opd_FUN_00a8b7c4();
  _opd_FUN_000482b0(0x60);
  _opd_FUN_00a2ddf4();
  _opd_FUN_000482b0(0x499);
  iVar4 = _opd_FUN_0097c124();
  if (*(char *)(iVar4 + 0x2b4) == '\n') {
    _opd_FUN_0099b4ec(0);
  }
  else {
    _opd_FUN_0099b9d4(0);
  }
  puVar2 = *(undefined4 **)(puVar3 + -0x8000);
  _opd_FUN_000c1e00(*puVar2);
  *puVar2 = 0;
  return;
}

