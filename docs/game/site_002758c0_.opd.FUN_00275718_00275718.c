// hook SITE 0x002758c0

undefined8 _opd_FUN_00275718(undefined4 param_1,undefined4 param_2,undefined2 param_3)

{
  ulonglong uVar1;
  ulonglong uVar2;
  ulonglong uVar3;
  uint uVar4;
  int iVar5;
  undefined4 uVar6;
  longlong lVar7;
  byte local_f0;
  int local_ec;
  byte local_b8;
  uint local_b0;
  undefined4 local_60;
  undefined1 local_5c;
  undefined4 local_54;
  undefined4 local_50;
  undefined4 local_4c;
  undefined4 local_48;
  
  uVar3 = ZEXT48(&stack0x00000000);
  uVar1 = uVar3 - 0xb8 & 0xffffffff;
  _opd_FUN_00fa4d48(uVar1,0,0x74);
  uVar2 = uVar3 - 0xec & 0xffffffff;
  _opd_FUN_0026cc88(param_2,uVar1,1);
  _opd_FUN_0026cc88(param_2,uVar2,4);
  _opd_FUN_0026cc88(param_2,uVar3 - 0xb0,4);
  uVar4 = _opd_FUN_0026d640();
  if ((uVar4 & 2) == 0) {
    iVar5 = _opd_FUN_0026c280();
    iVar5 = _opd_FUN_00fa4e20(uVar2,iVar5 + 2,4);
    if (iVar5 == 0) goto LAB_00275a7c;
  }
  else {
    iVar5 = _opd_FUN_00280418();
    if (*(int *)(&DAT_00015710 + iVar5) == local_ec) {
LAB_00275a7c:
      local_b0 = local_b0 | 2;
    }
  }
  _opd_FUN_0026cc88(param_2,uVar3 - 0x52,2);
  _opd_FUN_0026cc88(param_2,uVar3 - 0xf0,1);
  if (local_f0 < 3) {
    local_54 = (uint)CONCAT12(local_f0,local_54._2_2_);
    if (local_f0 == 0) goto LAB_0027588c;
  }
  else {
    local_f0 = 2;
    local_54 = CONCAT22(2,local_54._2_2_);
  }
  uVar4 = 0;
  do {
    uVar1 = (ulonglong)uVar4;
    uVar2 = (ulonglong)uVar4;
    uVar4 = uVar4 + 1;
    lVar7 = (uVar2 & 0x1fffffff) * 8 + (uVar1 & 0x7fffffff) * -2 + 0x60 + (uVar3 - 0xb8);
    _opd_FUN_0026cb20(param_2,lVar7 + 8U & 0xfffffffe,4);
    _opd_FUN_0026cc88(param_2,lVar7 + 0xcU & 0xfffffffe,2);
  } while ((int)uVar4 < (int)(uint)local_f0);
LAB_0027588c:
  uVar1 = uVar3 - 0xe8 & 0xffffffff;
  _opd_FUN_0026cc88(param_2,uVar3 - 0x60,4);
  _opd_FUN_0026cc88(param_2,uVar3 - 0x5c,1);
  uVar2 = uVar3 - 0x94 & 0xffffffff;
  _opd_FUN_00fa4d48(uVar1,0,0x30);
  _opd_FUN_0026cb20(param_2,uVar1,0x30);
  _opd_FUN_00f9e1b8(uVar2,uVar1,0x18);
  lVar7 = _opd_FUN_00f9dde8(uVar1);
  uVar1 = uVar3 - 0x78 & 0xffffffff;
  _opd_FUN_00f9e1b8(uVar1,lVar7 + (uVar3 - 0xe8) + 1 & 0xffffffff,0x18);
  _opd_FUN_0026f290(local_b8,local_b0,uVar2,0);
  iVar5 = _opd_FUN_0026d5a8(local_b8);
  *(undefined4 *)(iVar5 + 0x58) = local_60;
  *(undefined1 *)(iVar5 + 0x5c) = local_5c;
  _opd_FUN_00f9dca0(iVar5 + 0x40,uVar1);
  _opd_FUN_00282ba8(param_1,iVar5 + 0x14,iVar5,param_3);
  *(uint *)(iVar5 + 100) = local_54;
  *(undefined4 *)(iVar5 + 0x68) = local_50;
  *(undefined4 *)(iVar5 + 0x6c) = local_4c;
  *(int *)(iVar5 + 0x60) = local_ec;
  *(undefined4 *)(iVar5 + 0x70) = local_48;
  uVar6 = _opd_FUN_0027e2b0(local_b8 + 1);
  _opd_FUN_0027e578(uVar6,0x114,4,uVar3 - 0xec);
  _opd_FUN_0027f100(local_b8);
  _opd_FUN_002834a0(uVar3 - 0xa4);
  return 0;
}

