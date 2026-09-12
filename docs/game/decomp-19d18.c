// function FUN_00019d18 @ 00019d18 (target 19d18)

void FUN_00019d18(void)

{
  undefined8 uVar1;
  int iVar3;
  longlong lVar2;
  int iVar4;
  uint uVar5;
  undefined8 uVar6;
  int *piVar7;
  undefined1 auStack_160 [256];
  undefined1 auStack_60 [72];
  
  uVar1 = FUN_00011d58();
  FUN_00019c00(auStack_60,uVar1);
  iVar3 = FUN_00012b58(auStack_160);
  lVar2 = FUN_0001bdb0();
  if (lVar2 == 0) {
    if (*(char *)uRam001ec3b0 == '\0') {
      if (*(char *)uRam001ec3c0 == '\0') {
        iVar4 = FUN_00011308();
        uVar1 = uRam001ec3a8;
        if (iVar4 != 0x20) {
          uVar1 = uRam001ec3a0;
        }
        FUN_00010f70(auStack_160,0x100,uRam001ec3d0,uVar1);
      }
      else {
        iVar4 = FUN_00011308();
        uVar1 = uRam001ec3a8;
        if (iVar4 != 0x20) {
          uVar1 = uRam001ec3a0;
        }
        FUN_00010f70(auStack_160,0x100,uRam001ec3c8,uVar1);
      }
    }
    else {
      iVar4 = FUN_000112e8();
      uVar1 = uRam001ec3a0;
      if (iVar4 != 0x40) {
        uVar1 = uRam001ec3a8;
      }
      iVar4 = FUN_00011308();
      uVar6 = uRam001ec3a8;
      if (iVar4 != 0x20) {
        uVar6 = uRam001ec3a0;
      }
      FUN_00010f70(auStack_160,0x100,uRam001ec3b8,uVar1,uVar6);
    }
  }
  piVar7 = (int *)uRam001ec3d8;
  iVar4 = *piVar7;
  FUN_000124a0(iVar3 + 5,iVar4,0x34b - (iVar3 + 5),0x200 - iVar4);
  iVar3 = FUN_00012b58(auStack_160);
  uVar5 = 0x350 - iVar3;
  FUN_00012640(((int)uVar5 >> 1) + (uint)((int)uVar5 < 0 && (uVar5 & 1) != 0),*piVar7,1000,0xffffff,
               auStack_160);
  FUN_000124e0();
  return;
}

