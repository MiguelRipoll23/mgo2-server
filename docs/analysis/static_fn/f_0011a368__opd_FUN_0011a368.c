/* containing function of static patch target(s); entry .opd.FUN_0011a368 @ 0011a368 */

void _opd_FUN_0011a368(int param_1,undefined4 param_2)

{
  int iVar1;
  uint uVar2;
  int iVar3;
  undefined4 *puVar4;
  undefined4 uVar5;
  undefined *puVar6;
  ulonglong uVar7;
  undefined4 *puVar8;
  
  puVar6 = PTR_PTR_0121ad20;
  iVar1 = **(int **)(PTR_PTR_0121ad20 + -0x7ff8);
  *(undefined4 *)(iVar1 + 0x1d4) = 0xffffffef;
  *(undefined4 *)(iVar1 + 0x1dc) = 1;
  *(undefined4 *)(iVar1 + 0x1e0) = 1;
  *(undefined4 *)(iVar1 + 0x1ec) = 0;
  *(undefined4 *)(iVar1 + 0x1d0) = 0xe000;
  uVar2 = *(uint *)(param_1 + 0x394);
  *(undefined4 *)(iVar1 + 0x1fc) = 0;
  *(uint *)(iVar1 + 0x1f0) = uVar2 & 0x10;
  *(undefined4 *)(iVar1 + 500) = 2;
  *(undefined4 *)(iVar1 + 0x1f8) = 0;
  _opd_FUN_0015fe08(param_1 + 0x140);
  iVar1 = *(int *)(puVar6 + -0x7ffc);
  iVar3 = *(int *)(puVar6 + -0x7fec);
  puVar4 = *(undefined4 **)(puVar6 + -0x7f44);
  **(undefined4 **)(iVar1 + 8) = 0x40a6c;
  *(undefined4 *)(*(int *)(iVar1 + 8) + 4) = 0x206;
  puVar8 = (undefined4 *)(*(int *)(iVar1 + 8) + 8);
  *(undefined4 **)(iVar1 + 8) = puVar8;
  *puVar8 = 0x40a70;
  *(undefined4 *)(*(int *)(iVar1 + 8) + 4) = 1;
  puVar8 = (undefined4 *)(*(int *)(iVar1 + 8) + 8);
  *(undefined4 **)(iVar1 + 8) = puVar8;
  *puVar8 = 0x40a74;
  *(undefined4 *)(*(int *)(iVar1 + 8) + 4) = 1;
  uVar7 = *(ulonglong *)(iVar3 + 0x40);
  uVar5 = *puVar4;
  *(int *)(iVar1 + 8) = *(int *)(iVar1 + 8) + 8;
  *(ulonglong *)(iVar3 + 0x40) = uVar7 | 0xc000000;
  _opd_FUN_00118fe8(param_1,param_2,uVar5);
  _opd_FUN_0015fe08(param_1 + 0xc0);
  return;
}

