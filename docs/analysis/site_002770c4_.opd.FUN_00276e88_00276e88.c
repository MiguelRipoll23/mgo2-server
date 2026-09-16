// hook SITE 0x002770c4

undefined8 _opd_FUN_00276e88(undefined8 param_1,undefined4 param_2,byte *param_3)

{
  bool bVar1;
  undefined1 *puVar2;
  undefined *puVar3;
  int iVar4;
  undefined4 uVar5;
  undefined4 uVar6;
  byte *pbVar7;
  undefined4 uVar8;
  byte *pbVar9;
  undefined1 *puVar10;
  undefined1 *puVar11;
  undefined1 *puVar12;
  undefined1 *puVar13;
  undefined1 *puVar14;
  uint uVar15;
  undefined1 *puVar16;
  byte local_240;
  undefined1 local_23f [7];
  int local_238;
  int local_234;
  int local_230 [2];
  undefined1 uStack_228;
  undefined1 auStack_227 [47];
  undefined1 auStack_1f8 [2];
  undefined1 auStack_1f6 [2];
  undefined1 uStack_1f4;
  undefined1 uStack_1f3;
  byte local_1f2 [2];
  undefined4 local_1f0;
  undefined1 local_1ec [4];
  undefined1 auStack_1e8 [23];
  char local_1d1;
  char acStack_1d0 [290];
  undefined1 uStack_ae;
  undefined1 uStack_ad;
  undefined1 uStack_ac;
  undefined1 uStack_ab;
  undefined1 uStack_aa;
  undefined1 uStack_a9;
  undefined1 uStack_a8;
  undefined1 uStack_a7;
  undefined1 uStack_a6;
  undefined1 uStack_a5;
  undefined1 uStack_a4;
  undefined1 uStack_a3;
  undefined1 uStack_a2;
  undefined1 uStack_a1;
  undefined1 uStack_a0;
  undefined1 uStack_9f;
  undefined1 uStack_9e;
  undefined1 uStack_9d;
  undefined1 uStack_9c;
  undefined1 uStack_9b;
  undefined1 uStack_9a;
  undefined1 uStack_99;
  undefined1 uStack_98;
  undefined1 auStack_97 [15];
  
  puVar3 = PTR_PTR_0121af68;
  if (*(short *)(param_3 + 2) == 0x211) {
    puVar10 = &stack0x00000000 + -0x1f8;
    puVar14 = &stack0x00000000 + -0x1f6;
    _opd_FUN_00fa4d48(puVar10,0,0x164);
    puVar13 = &stack0x00000000 + -500;
    _opd_FUN_0026cc88(param_2,puVar10,2);
    puVar12 = &stack0x00000000 + -499;
    _opd_FUN_0026cc88(param_2,puVar14,2);
    puVar11 = &stack0x00000000 + -0x1e8;
    _opd_FUN_0026cc88(param_2,puVar13,1);
    puVar10 = &stack0x00000000 + -0x1d1;
    _opd_FUN_0026cc88(param_2,puVar12,1);
    _opd_FUN_0026cc88(param_2,&stack0x00000000 + -0x1f2,1);
    _opd_FUN_0026cc88(param_2,&stack0x00000000 + -0x1f0,4);
    _opd_FUN_0026cc88(param_2,&stack0x00000000 + -0x1ec,4);
    _opd_FUN_0026cb20(param_2,puVar11,0x17);
    _opd_FUN_0026cc88(param_2,puVar10,1);
    if (local_1d1 != '\0') {
      _opd_FUN_0026cc88(param_2,&stack0x00000000 + -0x1d0,1);
    }
    _opd_FUN_0026cc88(param_2,&stack0x00000000 + -0x1cf,1);
    if (acStack_1d0[1] != '\0') {
      _opd_FUN_0026cc88(param_2,&stack0x00000000 + -0x1ce,1);
    }
    _opd_FUN_0026cc88(param_2,&stack0x00000000 + -0x1cd,1);
    if (acStack_1d0[3] != '\0') {
      _opd_FUN_0026cc88(param_2,&stack0x00000000 + -0x1cc,1);
    }
    _opd_FUN_0026cc88(param_2,&stack0x00000000 + -0x1cb,1);
    if (acStack_1d0[5] != '\0') {
      _opd_FUN_0026cc88(param_2,&stack0x00000000 + -0x1ca,1);
    }
    _opd_FUN_0026cc88(param_2,&stack0x00000000 + -0x1c9,1);
    if (acStack_1d0[7] != '\0') {
      _opd_FUN_0026cc88(param_2,&stack0x00000000 + -0x1c8,1);
    }
    _opd_FUN_0026cc88(param_2,&stack0x00000000 + -0x240,1);
    if (local_240 != 0) {
      iVar4 = 1;
      puVar16 = &stack0x00000000 + -0x1c4;
      do {
        iVar4 = iVar4 + 1;
        _opd_FUN_0026cc88(param_2,puVar16,2);
        puVar16 = puVar16 + 2;
      } while (iVar4 <= (int)(uint)local_240);
    }
    puVar16 = &stack0x00000000 + -0x228;
    _opd_FUN_00fa4d48(puVar16,0,0x30);
    puVar2 = &stack0x00000000 + -0xae;
    _opd_FUN_0026cb20(param_2,puVar16,0x30);
    pbVar9 = param_3 + 0x24;
    param_3[0x3b] = 0;
    *pbVar9 = 0;
    param_3[0x25] = 0;
    param_3[0x26] = 0;
    param_3[0x27] = 0;
    param_3[0x28] = 0;
    param_3[0x29] = 0;
    param_3[0x2a] = 0;
    param_3[0x2b] = 0;
    param_3[0x2c] = 0;
    param_3[0x2d] = 0;
    param_3[0x2e] = 0;
    param_3[0x2f] = 0;
    param_3[0x30] = 0;
    param_3[0x31] = 0;
    param_3[0x32] = 0;
    param_3[0x33] = 0;
    param_3[0x34] = 0;
    param_3[0x35] = 0;
    param_3[0x36] = 0;
    param_3[0x37] = 0;
    param_3[0x38] = 0;
    param_3[0x39] = 0;
    param_3[0x3a] = 0;
    (&stack0x00000000)[-0xae] = 0;
    (&stack0x00000000)[-0xad] = 0;
    (&stack0x00000000)[-0xac] = 0;
    (&stack0x00000000)[-0xab] = 0;
    (&stack0x00000000)[-0xaa] = 0;
    (&stack0x00000000)[-0xa9] = 0;
    (&stack0x00000000)[-0xa8] = 0;
    (&stack0x00000000)[-0xa7] = 0;
    (&stack0x00000000)[-0xa6] = 0;
    (&stack0x00000000)[-0xa5] = 0;
    (&stack0x00000000)[-0x97] = 0;
    (&stack0x00000000)[-0xa4] = 0;
    (&stack0x00000000)[-0xa3] = 0;
    (&stack0x00000000)[-0xa2] = 0;
    (&stack0x00000000)[-0xa1] = 0;
    (&stack0x00000000)[-0xa0] = 0;
    (&stack0x00000000)[-0x9f] = 0;
    (&stack0x00000000)[-0x9e] = 0;
    (&stack0x00000000)[-0x9d] = 0;
    (&stack0x00000000)[-0x9c] = 0;
    (&stack0x00000000)[-0x9b] = 0;
    (&stack0x00000000)[-0x9a] = 0;
    (&stack0x00000000)[-0x99] = 0;
    (&stack0x00000000)[-0x98] = 0;
    _opd_FUN_00f9e1b8(pbVar9,puVar16,0x17);
    iVar4 = _opd_FUN_00f9dde8(puVar16);
    uVar15 = 0;
    _opd_FUN_00f9e1b8(puVar2,&stack0x00000000 + iVar4 + -0x227,0x17);
    *(undefined4 *)(param_3 + 0x58) = local_1f0;
    param_3[0x5c] = local_1f2[0];
    _opd_FUN_00f9dca0(param_3 + 0x40,puVar2);
    uVar5 = _opd_FUN_0027e2b0(*param_3 + 1);
    _opd_FUN_0027e578(uVar5,300,2,&stack0x00000000 + -0x1f8);
    _opd_FUN_0027e578(uVar5,0x12e,2,puVar14);
    _opd_FUN_0027e578(uVar5,0x126,1,puVar13);
    _opd_FUN_0027e578(uVar5,0x130,1,puVar12);
    _opd_FUN_0027e578(uVar5,0x131,0x17,puVar11);
    uVar6 = _opd_FUN_0027e2b0(*param_3 + 1);
    _opd_FUN_0027e480(uVar6,0x114,4,&stack0x00000000 + -0x234);
    _opd_FUN_0027e578(uVar6,0x128,4,&stack0x00000000 + -0x23c);
    do {
      pbVar7 = (byte *)_opd_FUN_0026d5a8(uVar15 & 0xff);
      if (((pbVar7 != (byte *)0x0) && (param_3 != pbVar7)) && (0x211 < *(ushort *)(pbVar7 + 2))) {
        uVar8 = _opd_FUN_0027e2b0(*pbVar7 + 1);
        _opd_FUN_0027e480(uVar8,0x114,4,&stack0x00000000 + -0x238);
        if ((local_238 != 0) &&
           (_opd_FUN_0027e480(uVar8,0x128,4,&stack0x00000000 + -0x230), local_234 == local_230[0]))
        {
          _opd_FUN_0027e578(uVar8,0x127,1,param_3);
        }
      }
      bVar1 = uVar15 != 0x17;
      uVar15 = uVar15 + 1;
    } while (bVar1);
    _opd_FUN_0027e578(uVar6,0x127,1,&stack0x00000000 + -0x23f);
    puVar11 = &stack0x00000000 + -0x1c6;
    _opd_FUN_0027bff8(*param_3,puVar10,5);
    _opd_FUN_0027e578(uVar5,0x152,0x100,puVar11);
    _opd_FUN_0027e578(uVar5,0x252,0x100,puVar11);
    _opd_FUN_0027e578(uVar5,2,0x18,pbVar9);
    param_3[2] = 2;
    param_3[3] = 0x12;
    if ((*(uint *)(param_3 + 8) & 0x4000) == 0) {
      _opd_FUN_00282c88((ulonglong)*(uint *)(puVar3 + -0x8000) + 0x18,param_3 + 0x14,param_3,
                        *param_3);
    }
    else {
      _opd_FUN_00281a50(**(undefined4 **)(puVar3 + -0x7fdc),param_3);
    }
    _opd_FUN_00279c48(1 << (*param_3 & 0x3f),1);
  }
  return 0;
}

