// hook SITE 0x0028137c

void _opd_FUN_00280b68(void)

{
  int *piVar1;
  int iVar2;
  int iVar4;
  uint uVar5;
  ulonglong uVar3;
  undefined4 uVar6;
  undefined4 uVar7;
  int *piVar8;
  byte bVar9;
  int iVar10;
  uint uVar11;
  uint uVar12;
  longlong lVar13;
  uint local_100;
  uint local_fc;
  undefined1 local_f8 [4];
  int local_f4;
  uint local_f0;
  uint local_ec [39];
  
  piVar1 = *(int **)(PTR_PTR_0121af7c + -0x8000);
  _opd_FUN_00f00d30(*piVar1);
  uVar5 = piVar1[4];
  if ((uVar5 & 1) != 0) {
    if ((uVar5 & 0x20000000) == 0) {
      if (((uVar5 & 0x8000000) != 0) && (iVar4 = _opd_FUN_00f13a30(*piVar1), iVar4 != 0)) {
        piVar1[4] = piVar1[4] & 0xf7ffffff;
        _opd_FUN_00f13944(*piVar1);
      }
    }
    else {
      iVar4 = _opd_FUN_00f13a38(*piVar1);
      if (iVar4 != 0) {
        piVar1[4] = piVar1[4] & 0xdfffffff;
        _opd_FUN_00f1394c(*piVar1);
      }
    }
    if ((piVar1[4] & 0x10000000U) == 0) {
      if (((piVar1[4] & 0x4000000U) != 0) && (iVar4 = _opd_FUN_00f139f0(*piVar1), iVar4 != 0)) {
        piVar1[4] = piVar1[4] & 0xfbffffff;
        _opd_FUN_00f13904(*piVar1);
      }
    }
    else {
      iVar4 = _opd_FUN_00f139f8(*piVar1);
      if (iVar4 != 0) {
        piVar1[4] = piVar1[4] & 0xefffffff;
        _opd_FUN_00f1390c(*piVar1);
      }
    }
    if ((piVar1[4] & 0x3c000000U) == 0) {
      _opd_FUN_0026d8e8(0x95);
      uVar6 = _opd_FUN_0027e2b0(0x19);
      _opd_FUN_0027e480(uVar6,0x104,4,&local_100);
      _opd_FUN_0027e480(uVar6,0x108,4,&local_f0);
      if ((local_f0 & local_100) != 0) {
        local_f0 = local_f0 & ~(local_f0 & local_100);
      }
      bVar9 = *(byte *)(piVar1 + 5);
      while (bVar9 < 0x18) {
        iVar4 = _opd_FUN_0026d5a8(bVar9);
        if (iVar4 != 0) {
          iVar4 = _opd_FUN_00f06488(*piVar1);
          uVar6 = _opd_FUN_0027e2b0(*(byte *)(piVar1 + 5) + 1);
          _opd_FUN_0027e480(uVar6,0x114,4,&local_f4);
          piVar8 = (int *)(iVar4 + 0x20);
          lVar13 = 0x40;
          do {
            iVar10 = *piVar8;
            piVar8 = piVar8 + 1;
            if (iVar10 == local_f4) {
              if ((1 << (*(byte *)(piVar1 + 5) & 0x3f) & local_100) == 0) {
                uVar3 = 0x10000000;
              }
              else {
                uVar3 = 0;
              }
              goto LAB_00280f4c;
            }
            lVar13 = lVar13 + -1;
          } while (lVar13 != 0);
          uVar5 = 1 << (*(byte *)(piVar1 + 5) & 0x3f) & local_100;
          uVar3 = (ulonglong)((int)uVar5 >> 0x1f);
          uVar3 = (uVar3 - (uVar3 ^ uVar5) & 0xffffffff) >> 2 & 0x20000000;
LAB_00280f4c:
          piVar8 = (int *)(iVar4 + 0x120);
          lVar13 = 0x40;
          do {
            iVar4 = *piVar8;
            piVar8 = piVar8 + 1;
            if (iVar4 == local_f4) {
              if ((1 << (*(byte *)(piVar1 + 5) & 0x3f) & local_f0) == 0) {
                uVar3 = uVar3 | 0x4000000;
              }
              goto LAB_00280f94;
            }
            lVar13 = lVar13 + -1;
          } while (lVar13 != 0);
          if ((1 << (*(byte *)(piVar1 + 5) & 0x3f) & local_f0) == 0) {
LAB_00280f94:
            if ((int)uVar3 == 0) goto LAB_00280f9c;
          }
          else {
            uVar3 = uVar3 | 0x8000000;
          }
          if (((uVar3 & 0x10000000) != 0) &&
             (iVar4 = _opd_FUN_00f14d00(*piVar1,local_f4), iVar4 != 0)) {
            uVar3 = uVar3 & 0xffffffffefffffff;
          }
          if (((uVar3 & 0x4000000) != 0) &&
             (iVar4 = _opd_FUN_00f14cf4(*piVar1,local_f4), iVar4 != 0)) {
            uVar3 = uVar3 & 0xfffffffff8000000;
          }
          if (((uVar3 & 0x20000000) != 0) &&
             (iVar4 = _opd_FUN_00f14e30(*piVar1,local_f4), iVar4 != 0)) {
            uVar3 = uVar3 & 0x1fffffff;
          }
          uVar5 = (uint)uVar3;
          if (((uVar3 & 0x8000000) != 0) &&
             (iVar4 = _opd_FUN_00f14e24(*piVar1,local_f4), iVar4 != 0)) {
            uVar3 = (uVar3 & 0x7ffffff) << 4 | uVar3 >> 0x1c;
            uVar5 = (uint)(uVar3 << 0x1c) | (uint)uVar3 >> 4;
          }
          piVar1[4] = uVar5 | piVar1[4];
          _opd_FUN_0026c9e0(piVar1 + 2);
          _opd_FUN_0026d990(0x95);
          *(char *)(piVar1 + 5) = *(char *)(piVar1 + 5) + '\x01';
          break;
        }
LAB_00280f9c:
        bVar9 = *(char *)(piVar1 + 5) + 1;
        *(byte *)(piVar1 + 5) = bVar9;
      }
      if ((*(char *)(piVar1 + 5) == '\x18') && ((piVar1[4] & 0x3c000000U) == 0)) {
        piVar1[4] = piVar1[4] & 0xfffffffe;
        uVar5 = 0;
        do {
          uVar12 = uVar5 + 1;
          iVar4 = _opd_FUN_0026d5a8(uVar5 & 0xff);
          if (iVar4 != 0) {
            iVar4 = _opd_FUN_00f06488(*piVar1);
            uVar6 = _opd_FUN_0027e2b0(uVar12);
            _opd_FUN_0027e480(uVar6,0x114,4,&local_f4);
            uVar6 = _opd_FUN_0027e2b0(0x19);
            _opd_FUN_0027e480(uVar6,0x104,4,local_f8);
            _opd_FUN_0027e480(uVar6,0x108,4,&local_fc);
            uVar5 = 1 << (uVar5 & 0x3f);
            local_fc = ~uVar5 & local_fc;
            iVar10 = 0;
            lVar13 = 0x40;
            do {
              iVar2 = iVar4 + iVar10;
              iVar10 = iVar10 + 4;
              if (*(int *)(iVar2 + 0x20) == local_f4) break;
              lVar13 = lVar13 + -1;
            } while (lVar13 != 0);
            iVar10 = 0;
            lVar13 = 0x40;
            do {
              iVar2 = iVar4 + iVar10;
              iVar10 = iVar10 + 4;
              if (*(int *)(iVar2 + 0x120) == local_f4) {
                local_fc = local_fc | uVar5;
                break;
              }
              lVar13 = lVar13 + -1;
            } while (lVar13 != 0);
            _opd_FUN_0027e578(uVar6,0x104,4,local_f8);
            _opd_FUN_0027e578(uVar6,0x108,4,&local_fc);
          }
          uVar5 = uVar12;
        } while (uVar12 != 0x18);
      }
    }
  }
  uVar5 = piVar1[4];
  if ((uVar5 & 2) != 0) {
    if ((uVar5 & 0x2000000) == 0) {
      piVar1[4] = uVar5 & 0xfffffffd;
    }
    else {
      iVar4 = _opd_FUN_00f06a18(*piVar1);
      if (iVar4 != 0) {
        _opd_FUN_00f06784(*piVar1);
      }
      _opd_FUN_0026d8e8(0x96);
      piVar1[4] = piVar1[4] & 0xfdffffff;
    }
  }
  if (*(int *)(*piVar1 + 0x88) < 0) {
    piVar1[4] = 0;
    return;
  }
  if ((piVar1[4] & 0xc0000000U) == 0) {
    uVar5 = _opd_FUN_0026c9f8(piVar1 + 2);
    if (28000 < uVar5) {
      uVar3 = _opd_FUN_0026d640();
      if ((uVar3 & 0x40000) == 0) {
        iVar4 = _opd_FUN_00f045a4(*piVar1);
        if (iVar4 == 0) {
          piVar1[4] = piVar1[4] | 0x80000000;
          _opd_FUN_0026d990(0x91);
        }
        _opd_FUN_0026c9e0(piVar1 + 2);
      }
      else {
        uVar6 = _opd_FUN_0027e2b0(0);
        uVar5 = 1;
        uVar12 = 0;
        do {
          iVar4 = _opd_FUN_0026d5a8(uVar12 & 0xff);
          if (iVar4 != 0) {
            uVar7 = _opd_FUN_0027e2b0(uVar12 + 1);
            _opd_FUN_0027e480(uVar7,0x11f,1,&local_100);
            _opd_FUN_0027e480(uVar7,0x120,1,&local_fc);
            if (local_100._0_1_ != local_fc._0_1_) {
              _opd_FUN_0027e480(uVar7,0x114,4,&local_f0);
              uVar11 = uVar5 + 1;
              local_ec[uVar5 * 2] = local_f0;
              local_ec[uVar5 * 2 + 1] = (uint)local_100._0_1_;
              _opd_FUN_0027e578(uVar7,0x120,1,&local_100);
              uVar5 = uVar11;
              if (0x11 < (int)uVar11) break;
            }
          }
          uVar12 = uVar12 + 1;
          uVar11 = uVar5;
        } while ((int)uVar12 < 0x18);
        _opd_FUN_0027e480(uVar6,0x59,1,&local_100);
        iVar4 = _opd_FUN_00f0ea88(*piVar1,local_100._0_1_,uVar11 & 0xff,local_ec);
        if (iVar4 == 0) {
          piVar1[4] = piVar1[4] | 0x40000000;
          _opd_FUN_0026d990(0x94);
        }
        _opd_FUN_0026c9e0(piVar1 + 2);
      }
    }
  }
  else if (piVar1[4] < 0) {
    iVar4 = _opd_FUN_00f04578();
    if (iVar4 != 0) {
      piVar1[4] = piVar1[4] & 0x7fffffff;
      _opd_FUN_0026d8e8(0x91);
    }
  }
  else {
    iVar4 = _opd_FUN_00f0d2b0();
    if (iVar4 != 0) {
      _opd_FUN_00f0cf14(*piVar1);
      piVar1[4] = piVar1[4] & 0xbfffffff;
      _opd_FUN_0026d8e8(0x94);
    }
  }
  return;
}

