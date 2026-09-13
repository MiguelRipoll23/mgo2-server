// hook SITE 0x00271450

undefined4 _opd_FUN_00270e00(undefined4 param_1,int *param_2,uint param_3,int param_4)

{
  bool bVar1;
  short sVar2;
  int *piVar3;
  uint uVar4;
  undefined4 uVar5;
  undefined4 uVar6;
  uint uVar7;
  uint uVar8;
  ulonglong uVar9;
  undefined *puVar10;
  undefined4 uVar11;
  uint uVar12;
  undefined4 *puVar13;
  int iVar14;
  int iVar15;
  int iVar16;
  ulonglong uVar17;
  byte *pbVar18;
  longlong lVar19;
  short *psVar20;
  int *piVar21;
  byte bVar23;
  ulonglong uVar22;
  byte bVar24;
  int iVar25;
  int iVar26;
  byte *pbVar27;
  byte *pbVar28;
  undefined1 local_320 [2];
  undefined2 local_31e;
  int iStack_31c;
  short local_316;
  undefined1 auStack_210 [384];
  undefined4 local_90;
  undefined4 local_8c [5];
  
  puVar10 = PTR_PTR_0121af64;
  piVar3 = *(int **)(PTR_PTR_0121af64 + -0x8000);
  iVar16 = *piVar3;
  if (*(int *)(iVar16 + 0xc0) != 0) {
    return 0xffffffff;
  }
  *(undefined1 *)(iVar16 + 0xca) = 0xff;
  *(undefined4 *)(iVar16 + 0xc4) = param_1;
  *(undefined1 *)(iVar16 + 200) = 0xff;
  *(undefined1 *)(iVar16 + 0xc9) = 0xff;
  _opd_FUN_00fa4d48(iVar16 + 0xd4,0,0xae0);
  iVar16 = *piVar3;
  *(uint *)(iVar16 + 0xbcc) = param_3;
  *(undefined4 *)(iVar16 + 0xc18) = 300;
  *(undefined1 *)(iVar16 + 0xc06) = 0;
  *(undefined4 *)(iVar16 + 0xd0) = 0;
  *(int **)(iVar16 + 0xbb4) = param_2;
  *(undefined4 *)(iVar16 + 0xc14) = 0;
  *(undefined4 *)(iVar16 + 0xc08) = 0;
  _opd_FUN_002749d8();
  if ((param_3 & 1) == 0) {
    iVar16 = *piVar3;
    _opd_FUN_00261ab0(&local_90,1,param_2);
    *(undefined4 *)(iVar16 + 3000) = local_90;
    iVar16 = *piVar3;
    if (*(int *)(iVar16 + 3000) != -1) {
      _opd_FUN_0026b2b0(iVar16 + 3000,0,3);
      _opd_FUN_0026b2b0(*piVar3 + 3000,3,100);
      iVar16 = *piVar3;
    }
    *(int *)(iVar16 + 0xc00) = iVar16 + 0xbd4;
    *(undefined4 *)(iVar16 + 0xc0) = 0x200;
    _opd_FUN_00274c90(iVar16 + 0xbec);
    _opd_FUN_0027a360(*(undefined4 *)(*piVar3 + 0xc00),0xb,*(undefined4 *)(puVar10 + -0x7fb0),0,1);
    _opd_FUN_0027a360(*(undefined4 *)(*piVar3 + 0xc00),8,*(undefined4 *)(puVar10 + -0x7fac),0,1);
    _opd_FUN_0027a360(*(undefined4 *)(*piVar3 + 0xc00),0xd,*(undefined4 *)(puVar10 + -0x7fa8),0,1);
    (*(code *)**(undefined4 **)(*param_2 + 0x1c))(param_2,&iStack_31c);
    if (local_316 == -1) {
      *(uint *)(*piVar3 + 0xbcc) = *(uint *)(*piVar3 + 0xbcc) | 8;
    }
  }
  else {
    iVar16 = *piVar3;
    *(undefined4 *)(iVar16 + 3000) = 0xffffffff;
    *(int *)(iVar16 + 0xc00) = iVar16 + 0xbec;
    _opd_FUN_00274d80();
    uVar11 = *(undefined4 *)(puVar10 + -0x7fb8);
    *(undefined4 *)(*piVar3 + 0xc0) = 0x100;
    _opd_FUN_002616c0(uVar11);
    iVar16 = *piVar3;
    uVar11 = _opd_FUN_00261fe0(0,2,0);
    *(undefined4 *)(iVar16 + 0xbd0) = uVar11;
    uVar11 = _opd_FUN_0027e2b0(0);
    local_31e = (undefined2)**(undefined4 **)(puVar10 + -0x7fb4);
    _opd_FUN_0027e578(uVar11,0xa2,2,&local_31e);
    *(undefined1 *)(*piVar3 + 0xbcb) = 0;
    _opd_FUN_00262960(0);
    _opd_FUN_0027e578(uVar11,0xa1,1,*piVar3 + 0xbcb);
  }
  if ((*(uint *)(*piVar3 + 0xbcc) & 2) != 0) {
    uVar11 = _opd_FUN_00280418();
    iVar16 = _opd_FUN_00f0cd6c(uVar11);
    if ((iVar16 != 0) && ((byte)(*(char *)(iVar16 + 8) - 2U) < 5)) {
      if (*(char *)(iVar16 + 8) != '\x02') {
        *(uint *)(*piVar3 + 0xbcc) = *(uint *)(*piVar3 + 0xbcc) | 0x700;
      }
      uVar11 = _opd_FUN_0027e2b0(0);
      local_31e = CONCAT11(*(undefined1 *)(iVar16 + 8),(undefined1)local_31e);
      _opd_FUN_0027e578(uVar11,0xa5,1,&local_31e);
    }
  }
  uVar11 = _opd_FUN_0027e2b0(0x19);
  _opd_FUN_0027e480(uVar11,0xb6,2,&local_31e);
  _opd_FUN_00261620(3,local_31e);
  *(undefined4 *)(*piVar3 + 0xcc) = 0;
  _opd_FUN_00277760();
  _opd_FUN_00281ca0(**(undefined4 **)(puVar10 + -0x7ff0));
  iVar16 = *piVar3;
  *(undefined2 *)(iVar16 + 0xbc8) = 0;
  *(undefined1 *)(iVar16 + 0xbca) = 0;
  *(undefined4 *)(iVar16 + 0xbbc) = 0;
  _opd_FUN_002756e0();
  _opd_FUN_00281478();
  _opd_FUN_0027d0f0();
  uVar11 = _opd_FUN_0027e2b0(0);
  _opd_FUN_0027e578(uVar11,99,1,local_320);
  if (param_4 == 0) goto LAB_00271118;
  if ((param_3 & 1) == 0) {
    if (*(int *)(*piVar3 + 3000) != -1) {
      _opd_FUN_0026cad8(&iStack_31c,auStack_210,0x174);
      _opd_FUN_0026cc10(&iStack_31c,local_320,1);
      _opd_FUN_0026cc10(&iStack_31c,param_4,2);
      _opd_FUN_0026cc10(&iStack_31c,param_4 + 2,2);
      _opd_FUN_0026cc10(&iStack_31c,param_4 + 4,1);
      _opd_FUN_0026cc10(&iStack_31c,param_4 + 5,1);
      _opd_FUN_0026cc10(&iStack_31c,param_4 + 6,1);
      _opd_FUN_0026cc10(&iStack_31c,param_4 + 8,4);
      _opd_FUN_0026cc10(&iStack_31c,param_4 + 0xc,4);
      _opd_FUN_0026cb98(&iStack_31c,param_4 + 0x10,0x17);
      _opd_FUN_0026cc10(&iStack_31c,(char *)(param_4 + 0x27),1);
      if (*(char *)(param_4 + 0x27) != '\0') {
        _opd_FUN_0026cc10(&iStack_31c,param_4 + 0x28,1);
      }
      _opd_FUN_0026cc10(&iStack_31c,(char *)(param_4 + 0x29),1);
      if (*(char *)(param_4 + 0x29) != '\0') {
        _opd_FUN_0026cc10(&iStack_31c,param_4 + 0x2a,1);
      }
      _opd_FUN_0026cc10(&iStack_31c,(char *)(param_4 + 0x2b),1);
      if (*(char *)(param_4 + 0x2b) != '\0') {
        _opd_FUN_0026cc10(&iStack_31c,param_4 + 0x2c,1);
      }
      _opd_FUN_0026cc10(&iStack_31c,(char *)(param_4 + 0x2d),1);
      if (*(char *)(param_4 + 0x2d) != '\0') {
        _opd_FUN_0026cc10(&iStack_31c,param_4 + 0x2e,1);
      }
      _opd_FUN_0026cc10(&iStack_31c,(char *)(param_4 + 0x2f),1);
      if (*(char *)(param_4 + 0x2f) != '\0') {
        _opd_FUN_0026cc10(&iStack_31c,param_4 + 0x30,1);
      }
      psVar20 = (short *)(param_4 + 0x34);
      bVar23 = 1;
      lVar19 = 0x7f;
      bVar24 = 0;
      do {
        sVar2 = *psVar20;
        psVar20 = psVar20 + 1;
        if (sVar2 != 0) {
          bVar24 = bVar23;
        }
        bVar23 = bVar23 + 1;
        lVar19 = lVar19 + -1;
      } while (lVar19 != 0);
      _opd_FUN_0026cc10(&iStack_31c,&local_31e,1);
      if (bVar24 != 0) {
        iVar14 = 1;
        iVar16 = param_4 + 0x34;
        do {
          iVar14 = iVar14 + 1;
          _opd_FUN_0026cc10(&iStack_31c,iVar16,2);
          iVar16 = iVar16 + 2;
        } while (iVar14 <= (int)(uint)bVar24);
      }
      iVar16 = _opd_FUN_00f9dde8(param_4 + 0x132);
      _opd_FUN_0026cb98(&iStack_31c,param_4 + 0x132,iVar16 + 1);
      uVar11 = _opd_FUN_00f9dde8(param_4 + 0x14a);
      _opd_FUN_0026cb98(&iStack_31c,param_4 + 0x14a,uVar11);
      _opd_FUN_0026b778(*piVar3 + 3000,&iStack_31c);
    }
    goto LAB_00271118;
  }
  if (*(int *)(*piVar3 + 0xd0) < 0x18) {
    uVar12 = _opd_FUN_00274a68();
    uVar8 = uVar12 & 0xff;
    if ((uVar8 != 0xff) && (uVar8 < 0x18)) {
      iVar16 = *piVar3;
      pbVar28 = (byte *)(iVar16 + 0xd4);
      lVar19 = 0x18;
      uVar7 = 1 << (uVar12 & 0x3f);
      do {
        if (*(short *)(pbVar28 + 2) == 0) {
          pbVar28[4] = 0;
          pbVar28[5] = 0;
          pbVar28[6] = 0;
          pbVar28[7] = 0;
          bVar24 = (byte)uVar12;
          *pbVar28 = bVar24;
          pbVar28[8] = 0;
          pbVar28[9] = 0;
          pbVar28[10] = 0;
          pbVar28[0xb] = 3;
          pbVar28[0x10] = 0xff;
          pbVar28[0x11] = 0xff;
          pbVar28[0x12] = 0xff;
          pbVar28[0x13] = 0xff;
          pbVar28[1] = 0xff;
          if ((*(uint *)(iVar16 + 0xbcc) & 0x201) != 0x201) goto LAB_00271658;
          iVar16 = _opd_FUN_00280418();
          _opd_FUN_00275050(pbVar28,pbVar28[1]);
          iVar14 = _opd_FUN_00f0cd6c(iVar16);
          if (iVar14 == 0) goto LAB_00271658;
          if ((*(uint *)(pbVar28 + 8) & 2) == 0) {
            iVar16 = (*(code *)**(undefined4 **)(iRam00000000 + 0x14))(0);
          }
          else {
            iVar16 = *(int *)(&DAT_00015710 + iVar16);
          }
          iVar26 = 0;
          lVar19 = 8;
          goto LAB_00271aa8;
        }
        pbVar28 = pbVar28 + 0x74;
        lVar19 = lVar19 + -1;
      } while (lVar19 != 0);
    }
  }
  pbVar27 = (byte *)(*piVar3 + 0xd4);
LAB_002710e0:
  lVar19 = 0x18;
  do {
    while (*pbVar27 != 0xff) {
      lVar19 = lVar19 + -1;
      pbVar27 = pbVar27 + 0x74;
      if (lVar19 == 0) goto LAB_00271108;
    }
    if (*(short *)(pbVar27 + 2) != 0) goto LAB_0027110c;
    lVar19 = lVar19 + -1;
    pbVar27 = pbVar27 + 0x74;
  } while (lVar19 != 0);
  goto LAB_00271108;
  while (lVar19 = lVar19 + -1, lVar19 != 0) {
LAB_00271aa8:
    iVar15 = iVar14 + iVar26;
    iVar26 = iVar26 + 4;
    if (*(int *)(iVar15 + 0x2c) == iVar16) {
      pbVar28[1] = 0;
      break;
    }
    if (*(int *)(iVar15 + 100) == iVar16) {
      pbVar28[1] = 1;
      break;
    }
  }
  _opd_FUN_00275050(pbVar28,pbVar28[1]);
LAB_00271658:
  pbVar28[0xc] = 0xff;
  uVar11 = _opd_FUN_0027e2b0(*pbVar28 + 1);
  _opd_FUN_0027e578(uVar11,0,1,pbVar28);
  _opd_FUN_0027e578(uVar11,1,1,pbVar28 + 1);
  _opd_FUN_0027e578(uVar11,2,0x18,param_4 + 0x132);
  uVar4 = *(uint *)(pbVar28 + 8);
  if ((uVar4 & 1) != 0) {
    if ((uVar4 & 0x200) == 0) {
      if ((*(uint *)(*piVar3 + 0xbcc) & 4) != 0) {
        *(uint *)(pbVar28 + 8) = uVar4 | 0x200;
      }
    }
    else {
      *(uint *)(*piVar3 + 0xbcc) = *(uint *)(*piVar3 + 0xbcc) | 4;
    }
    iVar16 = *piVar3;
    *(byte *)(iVar16 + 0xca) = bVar24;
    *(byte *)(iVar16 + 0xc9) = bVar24;
  }
  uVar4 = *(uint *)(pbVar28 + 8);
  if ((uVar4 & 2) == 0) {
    iVar16 = *piVar3;
    if ((*(uint *)(iVar16 + 0xbcc) & 1) == 0) {
      *(uint *)(pbVar28 + 8) = uVar4 | 4;
      if ((uVar4 & 1) == 0) {
        if ((uVar4 & 0x20000) == 0) {
          if ((*(uint *)(iVar16 + 0xbcc) & 8) == 0) {
            pbVar28[2] = 3;
            pbVar28[3] = 1;
          }
          else {
            pbVar28[2] = 3;
            pbVar28[3] = 2;
          }
          *(uint *)(iVar16 + 0xcc) = *(uint *)(iVar16 + 0xcc) | uVar7;
        }
        else {
          pbVar28[2] = 3;
          pbVar28[3] = 2;
        }
      }
      else {
        *(undefined4 *)(pbVar28 + 4) = *(undefined4 *)(iVar16 + 0xbb4);
        pbVar28[2] = 3;
        pbVar28[3] = 3;
        _opd_FUN_00279c48(uVar7,0);
        *(uint *)(*piVar3 + 0xcc) = uVar7 | *(uint *)(*piVar3 + 0xcc);
      }
    }
    else if ((uVar4 & 0x24000) == 0) {
      if ((*(uint *)(iVar16 + 0xbcc) & 2) == 0) {
        pbVar28[2] = 2;
        pbVar28[3] = 3;
      }
      else {
        pbVar28[2] = 2;
        pbVar28[3] = 1;
      }
      *(uint *)(pbVar28 + 8) = *(uint *)(pbVar28 + 8) | 0x2000;
    }
    else if ((uVar4 & 0x4000) == 0) {
      pbVar28[0x10] = 0xff;
      pbVar28[0x11] = 0xff;
      pbVar28[0x12] = 0xff;
      pbVar28[0x13] = 0xff;
      pbVar28[2] = 2;
      pbVar28[3] = 0x16;
    }
    else {
      pbVar28[2] = 2;
      pbVar28[3] = 0x11;
      _opd_FUN_00261ab0(local_8c,1,*(undefined4 *)(pbVar28 + 4));
      *(undefined4 *)(pbVar28 + 0x10) = local_8c[0];
      _opd_FUN_0026b2b0(pbVar28 + 0x10,0,3);
      _opd_FUN_0026b2b0(pbVar28 + 0x10,3,100);
    }
  }
  else {
    iVar16 = *piVar3;
    *(byte *)(iVar16 + 200) = bVar24;
    *(uint *)(pbVar28 + 8) = *(uint *)(pbVar28 + 8) | 4;
    if ((*(uint *)(iVar16 + 0xbcc) & 1) == 0) {
      pbVar28[2] = 3;
      pbVar28[3] = 3;
    }
    else {
      pbVar28[2] = 2;
      pbVar28[3] = 0x16;
      _opd_FUN_00275340(pbVar28);
      if ((*(uint *)(*piVar3 + 0xbcc) & 2) != 0) {
        iVar16 = _opd_FUN_00280418();
        uVar11 = _opd_FUN_0027e2b0(uVar8 + 1);
        _opd_FUN_0027e578(uVar11,0x114,4,&DAT_00015710 + iVar16);
      }
      _opd_FUN_0027f100(uVar12 & 0xff);
    }
    *(uint *)(*piVar3 + 0xcc) = *(uint *)(*piVar3 + 0xcc) | uVar7;
    _opd_FUN_00279c48(uVar7,0);
    puVar13 = (undefined4 *)_opd_FUN_00261730();
    uVar11 = puVar13[1];
    uVar5 = puVar13[2];
    uVar6 = puVar13[3];
    *(undefined4 *)(pbVar28 + 0x60) = *puVar13;
    *(undefined4 *)(pbVar28 + 100) = uVar11;
    *(undefined4 *)(pbVar28 + 0x68) = uVar5;
    *(undefined4 *)(pbVar28 + 0x6c) = uVar6;
    *(undefined4 *)(pbVar28 + 0x70) = puVar13[4];
  }
  piVar21 = *(int **)(pbVar28 + 4);
  if (piVar21 != (int *)0x0) {
    (*(code *)**(undefined4 **)(*piVar21 + 0x1c))(piVar21,pbVar28 + 0x60);
  }
  iVar14 = 0;
  _opd_FUN_00f9e1b8(pbVar28 + 0x24,param_4 + 0x132,0x18);
  iVar16 = *piVar3;
  *(int *)(pbVar28 + 0x3c) = 1 << (*pbVar28 & 0x3f) | 1 << (*(byte *)(iVar16 + 0xc9) & 0x3f);
  pbVar28[0xd] = *(byte *)(iVar16 + 0xc9);
  *(int *)(iVar16 + 0xd0) = *(int *)(iVar16 + 0xd0) + 1;
  _opd_FUN_00fa4d48(&iStack_31c,0,0x60);
  pbVar18 = (byte *)(*piVar3 + 0xd4);
  uVar17 = 0;
  lVar19 = 0x18;
  pbVar27 = pbVar18;
  do {
    sVar2 = *(short *)(pbVar27 + 2);
    if (((sVar2 != 0) && (sVar2 != 0x204)) && (sVar2 != 0x205)) {
      uVar17 = uVar17 | (uint)(1 << (*pbVar27 & 0x3f));
      iVar14 = iVar14 + (uint)((*(uint *)(pbVar27 + 8) & 2) == 0);
    }
    lVar19 = lVar19 + -1;
    pbVar27 = pbVar27 + 0x74;
  } while (lVar19 != 0);
  iVar16 = 0;
  iVar26 = 0;
  while( true ) {
    if (((*(short *)(pbVar18 + 2) != 0) && ((*(uint *)(pbVar18 + 8) & 0x2000) != 0)) &&
       (((*(uint *)(pbVar18 + 8) & 0x4000) == 0 &&
        (uVar22 = uVar17 & ~(ulonglong)(*(uint *)(pbVar18 + 0x3c) | 1 << (*pbVar18 & 0x3f)),
        (int)uVar22 != 0)))) {
      iVar15 = 0;
      lVar19 = 0x18;
      do {
        uVar9 = uVar22 & 1;
        piVar21 = (int *)((int)&iStack_31c + iVar15);
        uVar22 = uVar22 >> 1;
        iVar15 = iVar15 + 4;
        if (uVar9 != 0) {
          iVar26 = iVar26 + 1;
          *piVar21 = *piVar21 + 1;
        }
        lVar19 = lVar19 + -1;
      } while ((lVar19 != 0) && ((int)uVar22 != 0));
    }
    bVar1 = iVar16 == 0x17;
    iVar16 = iVar16 + 1;
    if (bVar1) break;
    pbVar18 = pbVar18 + 0x74;
  }
  if (iVar14 != 0) {
    iVar15 = _opd_FUN_00261688();
    iVar16 = *piVar3;
    if ((*(uint *)(iVar16 + 0xbcc) & 1) == 0) {
      iVar26 = 0;
      iVar16 = iVar16 + 0xd4;
      do {
        if (((*(short *)(iVar16 + 2) != 0) &&
            (piVar21 = *(int **)(iVar16 + 4), piVar21 != (int *)0x0)) &&
           ((*(uint *)(iVar16 + 8) & 0x4002) == 0)) {
          (*(code *)**(undefined4 **)(*piVar21 + 0x18))(piVar21,0,iVar15 / iVar14);
        }
        bVar1 = iVar26 != 0x17;
        iVar26 = iVar26 + 1;
        iVar16 = iVar16 + 0x74;
      } while (bVar1);
    }
    else if ((*(uint *)(iVar16 + 0xbcc) & 4) == 0) {
      iVar25 = 0;
      pbVar27 = (byte *)(iVar16 + 0xd4);
      do {
        if (((*(short *)(pbVar27 + 2) != 0) &&
            (piVar21 = *(int **)(pbVar27 + 4), piVar21 != (int *)0x0)) &&
           ((*(uint *)(pbVar27 + 8) & 0x4002) == 0)) {
          (*(code *)**(undefined4 **)(*piVar21 + 0x18))
                    (piVar21,0,(iVar15 / (iVar26 + iVar14 * 2)) * ((&iStack_31c)[*pbVar27] + 2));
        }
        bVar1 = iVar25 != 0x17;
        iVar25 = iVar25 + 1;
        pbVar27 = pbVar27 + 0x74;
      } while (bVar1);
    }
    else {
      iVar25 = 0;
      pbVar27 = (byte *)(iVar16 + 0xd4);
      do {
        if (((*(short *)(pbVar27 + 2) != 0) &&
            (piVar21 = *(int **)(pbVar27 + 4), piVar21 != (int *)0x0)) &&
           ((*(uint *)(pbVar27 + 8) & 0x4002) == 0)) {
          (*(code *)**(undefined4 **)(*piVar21 + 0x18))
                    (piVar21,0,(iVar15 / (iVar14 + iVar26)) * ((&iStack_31c)[*pbVar27] + 1));
        }
        bVar1 = iVar25 != 0x17;
        iVar25 = iVar25 + 1;
        pbVar27 = pbVar27 + 0x74;
      } while (bVar1);
    }
  }
  pbVar27 = (byte *)(*piVar3 + 0xd4);
  if (*pbVar28 == 0xff) goto LAB_002710e0;
  lVar19 = 0x18;
  do {
    if ((*pbVar27 == *pbVar28) &&
       ((*(short *)(pbVar27 + 2) != 0 || (*(byte *)(*piVar3 + 200) == *pbVar27))))
    goto LAB_0027110c;
    lVar19 = lVar19 + -1;
    pbVar27 = pbVar27 + 0x74;
  } while (lVar19 != 0);
LAB_00271108:
  pbVar27 = (byte *)0x0;
LAB_0027110c:
  _opd_FUN_002753c8(pbVar27,param_4);
LAB_00271118:
  _opd_FUN_00280938(0);
  _opd_FUN_00289228(0);
  return 0;
}

