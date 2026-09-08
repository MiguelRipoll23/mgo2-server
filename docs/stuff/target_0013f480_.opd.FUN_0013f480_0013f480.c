// original call TARGET 0x0013f480

/* WARNING: Heritage AFTER dead removal. Example location: w0xffffff60 : 0x0013f5bc */
/* WARNING: Restarted to delay deadcode elimination for space: stack */

void _opd_FUN_0013f480(int param_1)

{
  int iVar1;
  int iVar2;
  undefined4 *puVar3;
  undefined *puVar4;
  undefined4 uVar5;
  undefined4 uVar6;
  undefined1 local_b0 [16];
  undefined1 local_a0 [4];
  undefined4 uStack_9c;
  undefined4 uStack_98;
  undefined4 auStack_90 [2];
  undefined4 uStack_88;
  undefined4 uStack_80;
  undefined4 uStack_7c;
  undefined4 uStack_70;
  undefined4 uStack_6c;
  undefined4 uStack_68;
  undefined4 uStack_64;
  
  puVar4 = PTR_PTR_0121ad40;
  puVar3 = (undefined4 *)(*(uint *)(PTR_PTR_0121ad40 + -0x7e9c) & 0xfffffff0);
  uVar5 = *puVar3;
  uStack_9c = puVar3[1];
  uStack_98 = puVar3[2];
  uVar6 = puVar3[3];
  _opd_FUN_0013b3a8(0x17,local_b0);
  iVar1 = *(int *)(puVar4 + -0x7ffc);
  **(undefined4 **)(iVar1 + 8) = &DAT_00441efc;
  *(undefined4 *)(*(int *)(iVar1 + 8) + 4) = 0x10;
  iVar2 = *(int *)(iVar1 + 8);
  *(undefined4 **)(iVar1 + 8) = (undefined4 *)(iVar2 + 8);
  *(undefined4 *)(iVar2 + 8) = uVar5;
  *(undefined4 *)(iVar2 + 0xc) = uStack_9c;
  *(undefined4 *)(iVar2 + 0x10) = uStack_98;
  *(undefined4 *)(iVar2 + 0x14) = uVar6;
  iVar2 = *(int *)(iVar1 + 8);
  *(undefined4 **)(iVar1 + 8) = (undefined4 *)(iVar2 + 0x10);
  *(undefined4 *)(iVar2 + 0x10) = auStack_90[0];
  *(undefined4 *)(iVar2 + 0x14) = 0;
  *(undefined4 *)(iVar2 + 0x18) = uStack_88;
  *(undefined4 *)(iVar2 + 0x1c) = 0x3f000000;
  iVar2 = *(int *)(iVar1 + 8);
  *(undefined4 **)(iVar1 + 8) = (undefined4 *)(iVar2 + 0x10);
  *(undefined4 *)(iVar2 + 0x10) = uStack_80;
  *(undefined4 *)(iVar2 + 0x14) = uStack_7c;
  *(undefined4 *)(iVar2 + 0x18) = 0;
  *(undefined4 *)(iVar2 + 0x1c) = 0x3f000000;
  iVar2 = *(int *)(iVar1 + 8);
  *(undefined4 **)(iVar1 + 8) = (undefined4 *)(iVar2 + 0x10);
  *(undefined4 *)(iVar2 + 0x10) = uStack_70;
  *(undefined4 *)(iVar2 + 0x14) = uStack_6c;
  *(undefined4 *)(iVar2 + 0x18) = uStack_68;
  *(undefined4 *)(iVar2 + 0x1c) = uStack_64;
  *(int *)(iVar1 + 8) = *(int *)(iVar1 + 8) + 0x10;
  _opd_FUN_0014f290(0x28,local_a0);
  _opd_FUN_0014f290(0x29,auStack_90);
  _opd_FUN_0014f290(0x2a,&uStack_80);
  _opd_FUN_0014f290(0x2b,&uStack_70);
  **(undefined4 **)(iVar1 + 8) = &DAT_00441efc;
  *(undefined4 *)(*(int *)(iVar1 + 8) + 4) = 0x18;
  iVar2 = *(int *)(iVar1 + 8);
  *(undefined4 **)(iVar1 + 8) = (undefined4 *)(iVar2 + 8);
  *(undefined4 *)(iVar2 + 8) = uVar5;
  *(undefined4 *)(iVar2 + 0xc) = uStack_9c;
  *(undefined4 *)(iVar2 + 0x10) = uStack_98;
  *(undefined4 *)(iVar2 + 0x14) = uVar6;
  iVar2 = *(int *)(iVar1 + 8);
  *(undefined4 **)(iVar1 + 8) = (undefined4 *)(iVar2 + 0x10);
  *(undefined4 *)(iVar2 + 0x10) = auStack_90[0];
  *(undefined4 *)(iVar2 + 0x14) = 0;
  *(undefined4 *)(iVar2 + 0x18) = uStack_88;
  *(undefined4 *)(iVar2 + 0x1c) = 0x3f000000;
  iVar2 = *(int *)(iVar1 + 8);
  *(undefined4 **)(iVar1 + 8) = (undefined4 *)(iVar2 + 0x10);
  *(undefined4 *)(iVar2 + 0x10) = uStack_80;
  *(undefined4 *)(iVar2 + 0x14) = uStack_7c;
  *(undefined4 *)(iVar2 + 0x18) = 0;
  *(undefined4 *)(iVar2 + 0x1c) = 0x3f000000;
  iVar2 = *(int *)(iVar1 + 8);
  *(undefined4 **)(iVar1 + 8) = (undefined4 *)(iVar2 + 0x10);
  *(undefined4 *)(iVar2 + 0x10) = uStack_70;
  *(undefined4 *)(iVar2 + 0x14) = uStack_6c;
  *(undefined4 *)(iVar2 + 0x18) = uStack_68;
  *(undefined4 *)(iVar2 + 0x1c) = uStack_64;
  *(int *)(iVar1 + 8) = *(int *)(iVar1 + 8) + 0x10;
  _opd_FUN_0014f290(0x2d,local_a0);
  _opd_FUN_0014f290(0x2e,auStack_90);
  _opd_FUN_0014f290(0x2f,&uStack_70);
  if ((*(uint *)(param_1 + 0x394) & 0x10000) != 0) {
    _opd_FUN_0010ea48(10,*(undefined4 *)(puVar4 + -0x7e7c));
    return;
  }
  _opd_FUN_0010ea48(10,*(undefined4 *)(puVar4 + -0x7e78));
  return;
}

