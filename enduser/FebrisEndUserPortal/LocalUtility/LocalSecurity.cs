// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.EnumLibrary;
using Febris.ModelLibrary.Models.DataModels;
using Febris.ModelLibrary.Models.UserModels;
using Febris.UserNode.DataAccessLayer.Queries.DataQueries;
using Febris.UserNode.DataAccessLayer.Queries.UserQueries;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Febris.UserNode.Portal.LocalUtility
{
    //***********************************************************************************************************************************
    //public interface
    //***********************************************************************************************************************************
    //public interface ILocalSecurity
    //{
    //    //string RandomeString(string occupation);
    //    //string PasswordRandomize();

    //}


    ////***********************************************************************************************************************************
    ////***********************************************************************************************************************************
    //public class LocalSecurity : ILocalSecurity
    //{
    //    //***********************************************************************************************************************************
    //    //***********************************************************************************************************************************

    //    private readonly ProfessionalQueries _professionalContext;
    //    private readonly UserQueries _userContext;
    //    private readonly UserManager<LocalApplicationUser> _userManager;


    //    public LocalSecurity(UserManager<LocalApplicationUser> userManager,
    //        ProfessionalQueries professionalContext,
    //        UserQueries userContext
    //        )
    //    {
    //        _professionalContext = professionalContext;
    //        _userManager = userManager;
    //        _userContext = userContext;
    //    }

    //    public LocalSecurity(string occupation)
    //    {
    //        this.occupationString = occupation;
    //    }
        
    //    public LocalSecurity()
    //    {
    //    }



    //    //public FebrisSecurity(ProfessionalAndUserHandling professionalAndUserHandling)
    //    //{
    //    //    _professionalAndUserHandling = professionalAndUserHandling;
    //    //}
    //    //public FebrisSecurity(){}

    //    //***********************************************************************************************************************************
    //    //***********************************************************************************************************************************
    //    [TempData]
    //    public string StatusMessage { get; set; }
    //    const string chars = "qwertyuiopasdfghjklzxcvbnmQWERTYUIOPASDFGHJKLZXCVBNM0123456789-_";
    //    private Random random = new Random();
    //    private string occupationString;
    //    //private FebrisDbContext context;



    //    //########################################################################################################
    //    //Authority checking
    //    //########################################################################################################
    //    #region Authority check
    //    //***********************************************************************************************************************************
    //    //check user permissions - Make sure this user has authority to change this user.    
    //    //may want to change Claims principal to ApplicationUser?
    //    //***********************************************************************************************************************************
    //    //public async Task<(bool IsFebris, bool ApprovedUser, string StatusMessage)> AuthorityCheck(ClaimsPrincipal user, Institution provider)
    //    //{
    //    //    try
    //    //    {
    //    //        bool isFebris = false;
    //    //        bool isApproved = false;
    //    //        bool connected = false;
    //    //        bool hasRole = false;
    //    //        Guid userId = new Guid(_userManager.GetUserId(user));
    //    //        if (Guid.Empty == userId)
    //    //        {
    //    //            StatusMessage = "Error: No one is signed in"; // maybe redirect to signin?
    //    //            return (false, false, StatusMessage);
    //    //        }
    //    //        UserActiveAsync(userId);
    //    //        UserPasswordExpiration(userId);
    //    //        isFebris = IsFebris(user);
    //    //        if (isFebris == true)
    //    //        {
    //    //            return (true, true, StatusMessage);
    //    //        }
    //    //        connected = await _context.InstitutionLinkedUser.Where(p => p.Institution.Id == provider.Id && p.UserId == userId).AnyAsync();
    //    //        hasRole = IsAdmin(user);
    //    //        if (connected != true || hasRole != true)
    //    //        {
    //    //            StatusMessage = "Error: You are not connected to this provider";
    //    //            return (false, false, StatusMessage);
    //    //        }
    //    //        else
    //    //        {
    //    //            isApproved = true;
    //    //        }
    //    //        return (isFebris, isApproved, StatusMessage);
    //    //    }
    //    //    catch (DbUpdateConcurrencyException)
    //    //    {
    //    //        StatusMessage = "Error: An error has occured while processing this request";
    //    //        return (false, false, StatusMessage);
    //    //    }
    //    //}
    //    ////***********************************************************************************************************************************
    //    ////check user permissions - Make sure this user has authority to change this user.    
    //    ////may want to change Claims principal to ApplicationUser?
    //    ////***********************************************************************************************************************************
    //    //public async Task<(bool IsFebris, bool ApprovedUser, string StatusMessage)> AuthorityCheck(ClaimsPrincipal user, Professional professional)
    //    //{
    //    //    try
    //    //    {
    //    //        bool isFebris = false;
    //    //        bool isApproved = false;
    //    //        bool hasAuthority = false;
    //    //        bool hasRole = false;
    //    //        Guid userId = new Guid(_userManager.GetUserId(user));
    //    //        if (Guid.Empty == userId)
    //    //        {
    //    //            StatusMessage = "Error: No one is signed in";
    //    //            return (false, false, StatusMessage);
    //    //        }
    //    //        UserActiveAsync(userId);
    //    //        UserPasswordExpiration(userId);
    //    //        isFebris = IsFebris(user);
    //    //        if (isFebris == true)
    //    //        {
    //    //            return (true, true, StatusMessage);
    //    //        }

    //    //        hasAuthority = UserHasAuthority(user, professional);
    //    //        hasRole = IsAdmin(user);
    //    //        if (hasAuthority != true || hasRole != true)
    //    //        {
    //    //            StatusMessage = "Error: You are not connected to this provider";
    //    //            return (false, false, StatusMessage);
    //    //        }
    //    //        else
    //    //        {
    //    //            isApproved = true;
    //    //        }
    //    //        return (isFebris, isApproved, StatusMessage);
    //    //    }
    //    //    catch (DbUpdateConcurrencyException)
    //    //    {
    //    //        StatusMessage = "Error: An error has occured while processing this request";
    //    //        return (false, false, StatusMessage);
    //    //    }
    //    //}
    //    ////***********************************************************************************************************************************
    //    ////check user permissions - Make sure this user has authority to change this user.    
    //    ////may want to change Claims principal to ApplicationUser?
    //    ////***********************************************************************************************************************************
    //    //public async Task<(bool IsFebris, bool ApprovedUser, string StatusMessage)> AuthorityCheck(ClaimsPrincipal user, Location location)
    //    //{
    //    //    try
    //    //    {
    //    //        bool isFebris = false;
    //    //        bool isApproved = false;
    //    //        bool connected = false;
    //    //        bool hasRole = false;
    //    //        Guid userId = new Guid(_userManager.GetUserId(user));

    //    //        if (Guid.Empty == userId)
    //    //        {
    //    //            StatusMessage = "Error: No one is signed in"; // maybe redirect to signin?
    //    //            return (false, false, StatusMessage);
    //    //        }
    //    //        UserActiveAsync(userId);
    //    //        UserPasswordExpiration(userId);
    //    //        isFebris = IsFebris(user);
    //    //        if (isFebris == true)
    //    //        {
    //    //            return (true, true, StatusMessage);
    //    //        }

    //    //        connected = await _context.LocationLinkedUser
    //    //            .Where(p => p.Location.Id == location.Id && p.UserId == userId)
    //    //            .AnyAsync();

    //    //        if (!connected)
    //    //        {
    //    //            List<Institution> providerList = await _context.InstitutionLinkedLocation
    //    //                .Include(p => p.Institution)
    //    //                .Where(l => l.Location == location)
    //    //                .Select(p => p.Institution)
    //    //                .ToListAsync();

    //    //            connected = await _context.InstitutionLinkedUser
    //    //                .Where(p => providerList.Any(x => x == p.Institution))
    //    //                .AnyAsync();
    //    //        }

    //    //        hasRole = IsAdmin(user);
    //    //        if (connected != true || hasRole != true)
    //    //        {
    //    //            StatusMessage = "Error: You are not connected to this provider";
    //    //            return (false, false, StatusMessage);
    //    //        }
    //    //        else
    //    //        {
    //    //            isApproved = true;
    //    //        }

    //    //        return (isFebris, isApproved, StatusMessage);
    //    //    }
    //    //    catch (DbUpdateConcurrencyException)
    //    //    {
    //    //        StatusMessage = "Error: An error has occured while processing this request";
    //    //        return (false, false, StatusMessage);
    //    //    }
    //    //}

    //    //public async Task<(bool IsFebris, bool AuthroizedUser, string StatusMessage)> AuthorityCheck(ClaimsPrincipal user, Professional professional, Location location, Institution provider)
    //    //{
    //    //    try
    //    //    {
    //    //        bool isFebris = false;
    //    //        bool isApproved = false;
    //    //        bool userToProviderConnection = false;
    //    //        bool userToLocationConnection = false;
    //    //        bool providerConnectedtoLocation = false;
    //    //        bool hasRole = false;
    //    //        bool userHasAuthority = false;
    //    //        Guid userId = new Guid(_userManager.GetUserId(user));
    //    //        UserActiveAsync(userId);
    //    //        UserPasswordExpiration(userId);
    //    //        if (Guid.Empty == userId)
    //    //        {
    //    //            StatusMessage = "Error: No one is signed in"; // maybe redirect to signin?
    //    //            return (false, false, StatusMessage);
    //    //        }
    //    //        isFebris = IsFebris(user);
    //    //        if (isFebris == true)
    //    //        {
    //    //            return (true, true, StatusMessage);
    //    //        }
    //    //        userToProviderConnection = IsProviderConnectedToUser(provider, userId);
    //    //        if (userToProviderConnection != true)
    //    //        {
    //    //            StatusMessage = "Error: You are not connected to this provider";
    //    //            return (false, false, StatusMessage);
    //    //        }
    //    //        userToLocationConnection = await _context.LocationLinkedUser
    //    //            .Where(l => l.Location == location && l.UserId == userId)
    //    //            .AnyAsync();
    //    //        if (userToProviderConnection != true)
    //    //        {
    //    //            providerConnectedtoLocation = await _context.InstitutionLinkedLocation
    //    //                .Where(p => p.Institution == provider && p.Location == location)
    //    //                .AnyAsync();
    //    //        }

    //    //        if (userToLocationConnection == false && providerConnectedtoLocation == false)
    //    //        {
    //    //            StatusMessage = "Error: You are not connected to this location";
    //    //            return (false, false, StatusMessage);
    //    //        }
    //    //        userHasAuthority = UserHasAuthority(user, professional);
    //    //        hasRole = IsAdmin(user);
    //    //        if (userHasAuthority != true)
    //    //        {
    //    //            StatusMessage = "You do not have authority to modify, add, or remove this professional";
    //    //            return (false, false, StatusMessage);
    //    //        }
    //    //        else if (userHasAuthority == true)
    //    //        {
    //    //            isApproved = true;
    //    //        }
    //    //        return (isFebris, isApproved, StatusMessage);
    //    //    }
    //    //    catch (DbUpdateConcurrencyException)
    //    //    {
    //    //        StatusMessage = "Error: An error has occured while processing this request";
    //    //        return (false, false, StatusMessage);
    //    //    }
    //    //}

    //    //***********************************************************************************************************************************
    //    //multi-permission check 
    //    //***********************************************************************************************************************************
    //    //public async Task<(bool IsFebris, bool AuthroizedUser, string StatusMessage)> AuthorityCheck(ClaimsPrincipal user, Professional professional, Institution provider)
    //    //{
    //    //    try
    //    //    {
    //    //        bool isFebris = false;
    //    //        bool isApproved = false;
    //    //        bool connection = false;
    //    //        bool userHasAuthority = false;
    //    //        bool hasRole = false;
    //    //        bool editingSelf = false;
    //    //        Guid userId = new Guid(_userManager.GetUserId(user));
    //    //        UserActiveAsync(userId);
    //    //        UserPasswordExpiration(userId);
    //    //        if (Guid.Empty == userId)
    //    //        {
    //    //            StatusMessage = "Error: No one is signed in";
    //    //            return (false, false, StatusMessage);
    //    //        }
    //    //        isFebris = IsFebris(user);
    //    //        if (isFebris == true)
    //    //        {
    //    //            return (true, true, StatusMessage);
    //    //        }
    //    //        editingSelf = PersonalAuthorityCheck(user, professional);
    //    //        if (editingSelf == true)
    //    //        {
    //    //            return (false, true, StatusMessage);
    //    //        }

    //    //        connection = await _context.InstitutionLinkedUser.Where(p => p.Institution.Id == provider.Id && p.UserId == userId).AnyAsync();
    //    //        if (connection != true)
    //    //        {
    //    //            StatusMessage = "Error: You are not connected to this provider";
    //    //            return (false, false, StatusMessage);
    //    //        }
    //    //        userHasAuthority = UserHasAuthority(user, professional);
    //    //        hasRole = IsAdmin(user);
    //    //        if (userHasAuthority != true || hasRole != true)
    //    //        {
    //    //            StatusMessage = "You do not have authority to modify, add, or remove this professional";
    //    //            return (false, false, StatusMessage);
    //    //        }
    //    //        else if (userHasAuthority == true)
    //    //        {
    //    //            isApproved = true;
    //    //        }
    //    //        return (isFebris, isApproved, StatusMessage);
    //    //    }
    //    //    catch (DbUpdateConcurrencyException)
    //    //    {
    //    //        StatusMessage = "Error: An error has occured while processing this request";
    //    //        return (false, false, StatusMessage);
    //    //    }
    //    //}

    //    #endregion

    //    #region High Clearance Authority Check
    //    //***********************************************************************************************************************************
    //    //check user permissions - Make sure this user has authority to change this user.    
    //    //may want to change Claims principal to ApplicationUser?
    //    //***********************************************************************************************************************************
    //    //public async Task<(bool IsFebris, bool ApprovedUser, string StatusMessage)> HighClearanceAuthorityCheck(ClaimsPrincipal user, Institution provider)
    //    //{
    //    //    try
    //    //    {
    //    //        bool isFebris = false;
    //    //        bool isApproved = false;
    //    //        bool connected = false;
    //    //        bool hasRole = false;
    //    //        Guid userId = new Guid(_userManager.GetUserId(user));
    //    //        if (Guid.Empty == userId)
    //    //        {
    //    //            StatusMessage = "Error: No one is signed in"; // maybe redirect to signin?
    //    //            return (false, false, StatusMessage);
    //    //        }
    //    //        UserActiveAsync(userId);
    //    //        UserPasswordExpiration(userId);
    //    //        isFebris = IsFebris(user);
    //    //        if (isFebris == true)
    //    //        {
    //    //            return (true, true, StatusMessage);
    //    //        }
    //    //        connected = await _context.InstitutionLinkedUser.Where(p => p.Institution.Id == provider.Id && p.UserId == userId).AnyAsync();
    //    //        hasRole = IsHigherAdmin(user);
    //    //        if (connected != true || hasRole != true)
    //    //        {
    //    //            StatusMessage = "Error: You are not connected to this provider";
    //    //            return (false, false, StatusMessage);
    //    //        }
    //    //        else
    //    //        {
    //    //            isApproved = true;
    //    //        }
    //    //        return (isFebris, isApproved, StatusMessage);
    //    //    }
    //    //    catch (DbUpdateConcurrencyException)
    //    //    {
    //    //        StatusMessage = "Error: An error has occured while processing this request";
    //    //        return (false, false, StatusMessage);
    //    //    }
    //    //}
    //    ////***********************************************************************************************************************************
    //    ////check user permissions - Make sure this user has authority to change this user.    
    //    ////may want to change Claims principal to ApplicationUser?
    //    ////***********************************************************************************************************************************
    //    //public async Task<(bool IsFebris, bool ApprovedUser, string StatusMessage)> HighClearanceAuthorityCheck(ClaimsPrincipal user, Professional professional)
    //    //{
    //    //    try
    //    //    {
    //    //        bool isFebris = false;
    //    //        bool isApproved = false;
    //    //        bool hasAuthority = false;
    //    //        bool hasRole = false;
    //    //        Guid userId = new Guid(_userManager.GetUserId(user));
    //    //        if (Guid.Empty == userId)
    //    //        {
    //    //            StatusMessage = "Error: No one is signed in";
    //    //            return (false, false, StatusMessage);
    //    //        }
    //    //        UserActiveAsync(userId);
    //    //        UserPasswordExpiration(userId);
    //    //        isFebris = IsFebris(user);
    //    //        if (isFebris == true)
    //    //        {
    //    //            return (true, true, StatusMessage);
    //    //        }

    //    //        hasAuthority = UserHasAuthority(user, professional);
    //    //        hasRole = IsHigherAdmin(user);
    //    //        if (hasAuthority != true || hasRole != true)
    //    //        {
    //    //            StatusMessage = "Error: You are not connected to this provider";
    //    //            return (false, false, StatusMessage);
    //    //        }
    //    //        else
    //    //        {
    //    //            isApproved = true;
    //    //        }
    //    //        return (isFebris, isApproved, StatusMessage);
    //    //    }
    //    //    catch (DbUpdateConcurrencyException)
    //    //    {
    //    //        StatusMessage = "Error: An error has occured while processing this request";
    //    //        return (false, false, StatusMessage);
    //    //    }
    //    //}
    //    ////***********************************************************************************************************************************
    //    ////check user permissions - Make sure this user has authority to change this user.    
    //    ////may want to change Claims principal to ApplicationUser?
    //    ////***********************************************************************************************************************************
    //    //public async Task<(bool IsFebris, bool ApprovedUser, string StatusMessage)> HighClearanceAuthorityCheck(ClaimsPrincipal user, Location location)
    //    //{
    //    //    try
    //    //    {
    //    //        bool isFebris = false;
    //    //        bool isApproved = false;
    //    //        bool connection = false;
    //    //        bool hasRole = false;
    //    //        Guid userId = new Guid(_userManager.GetUserId(user));

    //    //        if (Guid.Empty == userId)
    //    //        {
    //    //            StatusMessage = "Error: No one is signed in"; // maybe redirect to signin?
    //    //            return (false, false, StatusMessage);
    //    //        }
    //    //        UserActiveAsync(userId);
    //    //        UserPasswordExpiration(userId);
    //    //        isFebris = IsFebris(user);
    //    //        if (isFebris == true)
    //    //        {
    //    //            return (true, true, StatusMessage);
    //    //        }

    //    //        connection = await _context.LocationLinkedUser.Where(p => p.Location.Id == location.Id && p.UserId == userId).AnyAsync();
    //    //        if (!connection)
    //    //        {
    //    //            List<Institution> providerList = await _context.InstitutionLinkedLocation
    //    //                .Include(p => p.Institution)
    //    //                .Where(l => l.Location == location)
    //    //                .Select(p => p.Institution)
    //    //                .ToListAsync();

    //    //            connection = await _context.InstitutionLinkedUser
    //    //                .Where(p => providerList.Any(x => x == p.Institution))
    //    //                .AnyAsync();
    //    //        }

    //    //        hasRole = IsHigherAdmin(user);
    //    //        if (connection != true || hasRole != true)
    //    //        {
    //    //            StatusMessage = "Error: You are not connected to this provider";
    //    //            return (false, false, StatusMessage);
    //    //        }
    //    //        else//add in here
    //    //        {
    //    //            isApproved = true;
    //    //        }

    //    //        return (isFebris, isApproved, StatusMessage);
    //    //    }
    //    //    catch (DbUpdateConcurrencyException)
    //    //    {
    //    //        StatusMessage = "Error: An error has occured while processing this request";
    //    //        return (false, false, StatusMessage);
    //    //    }
    //    //}

    //    //public async Task<(bool IsFebris, bool AuthroizedUser, string StatusMessage)> HighClearanceAuthorityCheck(ClaimsPrincipal user, Professional professional, Location location, Institution provider)
    //    //{
    //    //    try
    //    //    {
    //    //        bool isFebris = false;
    //    //        bool isApproved = false;
    //    //        bool userToProviderConnection = false;
    //    //        bool userToLocationConnection = false;
    //    //        bool providerConnectedtoLocation = false;
    //    //        bool hasRole = false;
    //    //        bool userHasAuthority = false;
    //    //        Guid userId = new Guid(_userManager.GetUserId(user));
    //    //        UserActiveAsync(userId);
    //    //        UserPasswordExpiration(userId);
    //    //        if (Guid.Empty == userId)
    //    //        {
    //    //            StatusMessage = "Error: No one is signed in"; // maybe redirect to signin?
    //    //            return (false, false, StatusMessage);
    //    //        }
    //    //        isFebris = IsFebris(user);
    //    //        if (isFebris == true)
    //    //        {
    //    //            return (true, true, StatusMessage);
    //    //        }
    //    //        userToProviderConnection = IsProviderConnectedToUser(provider, userId);
    //    //        if (userToProviderConnection != true)
    //    //        {
    //    //            StatusMessage = "Error: You are not connected to this provider";
    //    //            return (false, false, StatusMessage);
    //    //        }
    //    //        userToLocationConnection = await _context.LocationLinkedUser
    //    //            .Where(l => l.Location == location && l.UserId == userId)
    //    //            .AnyAsync();
    //    //        if (userToProviderConnection != true)
    //    //        {
    //    //            providerConnectedtoLocation = await _context.InstitutionLinkedLocation
    //    //                .Where(p => p.Institution == provider && p.Location == location)
    //    //                .AnyAsync();
    //    //        }

    //    //        if (userToLocationConnection == false && providerConnectedtoLocation == false)
    //    //        {
    //    //            StatusMessage = "Error: You are not connected to this location";
    //    //            return (false, false, StatusMessage);
    //    //        }
    //    //        userHasAuthority = UserHasAuthority(user, professional);
    //    //        hasRole = IsHigherAdmin(user);
    //    //        if (userHasAuthority != true || hasRole != true)
    //    //        {
    //    //            StatusMessage = "You do not have authority to modify, add, or remove this professional";
    //    //            return (false, false, StatusMessage);
    //    //        }
    //    //        else if (userHasAuthority == true /*|| FebrisAuthorityCheck(user)*/)
    //    //        {
    //    //            isApproved = true;
    //    //        }
    //    //        return (isFebris, isApproved, StatusMessage);
    //    //    }
    //    //    catch (DbUpdateConcurrencyException)
    //    //    {
    //    //        StatusMessage = "Error: An error has occured while processing this request";
    //    //        return (false, false, StatusMessage);
    //    //    }
    //    //}

    //    ////***********************************************************************************************************************************
    //    ////multi-permission check 
    //    ////***********************************************************************************************************************************
    //    //public async Task<(bool IsFebris, bool AuthroizedUser, string StatusMessage)> HighClearanceAuthorityCheck(ClaimsPrincipal user, Professional professional, Institution provider)
    //    //{
    //    //    try
    //    //    {
    //    //        bool isFebris = false;
    //    //        bool isApproved = false;
    //    //        bool connection = false;
    //    //        bool userHasAuthority = false;
    //    //        bool hasRole = false;
    //    //        Guid userId = new Guid(_userManager.GetUserId(user));
    //    //        UserActiveAsync(userId);
    //    //        UserPasswordExpiration(userId);
    //    //        if (Guid.Empty == userId)
    //    //        {
    //    //            StatusMessage = "Error: No one is signed in";
    //    //            return (false, false, StatusMessage);
    //    //        }
    //    //        isFebris = IsFebris(user);
    //    //        if (isFebris == true)
    //    //        {
    //    //            return (true, true, StatusMessage);
    //    //        }
    //    //        connection = await _context.InstitutionLinkedUser.Where(p => p.Institution.Id == provider.Id && p.UserId == userId).AnyAsync();
    //    //        if (connection != true)
    //    //        {
    //    //            StatusMessage = "Error: You are not connected to this provider";
    //    //            return (false, false, StatusMessage);
    //    //        }
    //    //        userHasAuthority = UserHasAuthority(user, professional);
    //    //        hasRole = IsHigherAdmin(user);
    //    //        if (userHasAuthority != true || hasRole != true)
    //    //        {
    //    //            StatusMessage = "You do not have authority to modify, add, or remove this professional";
    //    //            return (false, false, StatusMessage);
    //    //        }
    //    //        else if (userHasAuthority == true || FebrisAuthorityCheck(user))
    //    //        {
    //    //            isApproved = true;
    //    //        }
    //    //        return (isFebris, isApproved, StatusMessage);
    //    //    }
    //    //    catch (DbUpdateConcurrencyException)
    //    //    {
    //    //        StatusMessage = "Error: An error has occured while processing this request";
    //    //        return (false, false, StatusMessage);
    //    //    }
    //    //}
    //    #endregion

    //    #region Highest Clearance Authority Check
    //    //***********************************************************************************************************************************
    //    //check user permissions - Make sure this user has authority to change this user.    
    //    //may want to change Claims principal to ApplicationUser?
    //    ////***********************************************************************************************************************************
    //    //public async Task<(bool IsFebris, bool ApprovedUser, string StatusMessage)> TopClearanceAuthorityCheck(ClaimsPrincipal user, Institution provider)
    //    //{
    //    //    try
    //    //    {
    //    //        bool isFebris = false;
    //    //        bool isApproved = false;
    //    //        bool connected = false;
    //    //        bool hasRole = false;
    //    //        Guid userId = new Guid(_userManager.GetUserId(user));
    //    //        if (Guid.Empty == userId)
    //    //        {
    //    //            StatusMessage = "Error: No one is signed in"; // maybe redirect to signin?
    //    //            return (false, false, StatusMessage);
    //    //        }
    //    //        UserActiveAsync(userId);
    //    //        UserPasswordExpiration(userId);
    //    //        isFebris = IsFebris(user);
    //    //        if (isFebris == true)
    //    //        {
    //    //            return (true, true, StatusMessage);
    //    //        }
    //    //        connected = await _context.InstitutionLinkedUser.Where(p => p.Institution.Id == provider.Id && p.UserId == userId).AnyAsync();
    //    //        hasRole = IsTopAdmin(user);
    //    //        if (connected != true || hasRole != true)
    //    //        {
    //    //            StatusMessage = "Error: You are not connected to this provider";
    //    //            return (false, false, StatusMessage);
    //    //        }
    //    //        else if (user.IsInRole(UserAccountType.ITAdmin.ToString()) || FebrisAuthorityCheck(user))
    //    //        {
    //    //            //is in role appropriate

    //    //            isApproved = true;
    //    //        }
    //    //        return (isFebris, isApproved, StatusMessage);
    //    //    }
    //    //    catch (DbUpdateConcurrencyException)
    //    //    {
    //    //        StatusMessage = "Error: An error has occured while processing this request";
    //    //        return (false, false, StatusMessage);
    //    //    }
    //    //}
    //    ////***********************************************************************************************************************************
    //    ////check user permissions - Make sure this user has authority to change this user.    
    //    ////may want to change Claims principal to ApplicationUser?
    //    ////***********************************************************************************************************************************
    //    //public async Task<(bool IsFebris, bool ApprovedUser, string StatusMessage)> TopClearanceAuthorityCheck(ClaimsPrincipal user, Professional professional)
    //    //{
    //    //    try
    //    //    {
    //    //        bool isFebris = false;
    //    //        bool isApproved = false;
    //    //        bool hasAuthority = false;
    //    //        bool hasRole = false;
    //    //        Guid userId = new Guid(_userManager.GetUserId(user));
    //    //        if (Guid.Empty == userId)
    //    //        {
    //    //            StatusMessage = "Error: No one is signed in";
    //    //            return (false, false, StatusMessage);
    //    //        }
    //    //        UserActiveAsync(userId);
    //    //        UserPasswordExpiration(userId);
    //    //        isFebris = IsFebris(user);
    //    //        if (isFebris == true)
    //    //        {
    //    //            return (true, true, StatusMessage);
    //    //        }

    //    //        hasAuthority = UserHasAuthority(user, professional);
    //    //        hasRole = IsTopAdmin(user);
    //    //        if (hasAuthority != true || hasRole != true)
    //    //        {
    //    //            StatusMessage = "Error: You are not connected to this provider";
    //    //            return (false, false, StatusMessage);
    //    //        }
    //    //        else if (user.IsInRole(UserAccountType.ITAdmin.ToString()) || FebrisAuthorityCheck(user))
    //    //        {
    //    //            isApproved = true;
    //    //        }
    //    //        return (isFebris, isApproved, StatusMessage);
    //    //    }
    //    //    catch (DbUpdateConcurrencyException)
    //    //    {
    //    //        StatusMessage = "Error: An error has occured while processing this request";
    //    //        return (false, false, StatusMessage);
    //    //    }
    //    //}
    //    ////***********************************************************************************************************************************
    //    ////check user permissions - Make sure this user has authority to change this user.    
    //    ////may want to change Claims principal to ApplicationUser?
    //    ////***********************************************************************************************************************************
    //    //public async Task<(bool IsFebris, bool ApprovedUser, string StatusMessage)> TopClearanceAuthorityCheck(ClaimsPrincipal user, Location location)
    //    //{
    //    //    try
    //    //    {
    //    //        bool isFebris = false;
    //    //        bool isApproved = false;
    //    //        bool connection = false;
    //    //        bool hasRole = false;
    //    //        Guid userId = new Guid(_userManager.GetUserId(user));

    //    //        if (Guid.Empty == userId)
    //    //        {
    //    //            StatusMessage = "Error: No one is signed in"; // maybe redirect to signin?
    //    //            return (false, false, StatusMessage);
    //    //        }
    //    //        UserActiveAsync(userId);
    //    //        UserPasswordExpiration(userId);
    //    //        isFebris = IsFebris(user);
    //    //        if (isFebris == true)
    //    //        {
    //    //            return (true, true, StatusMessage);
    //    //        }

    //    //        connection = await _context.LocationLinkedUser.Where(p => p.Location.Id == location.Id && p.UserId == userId).AnyAsync();
    //    //        if (!connection)
    //    //        {
    //    //            List<Institution> providerList = await _context.InstitutionLinkedLocation
    //    //                .Include(p => p.Institution)
    //    //                .Where(l => l.Location == location)
    //    //                .Select(p => p.Institution)
    //    //                .ToListAsync();

    //    //            connection = await _context.InstitutionLinkedUser
    //    //                .Where(p => providerList.Any(x => x == p.Institution))
    //    //                .AnyAsync();
    //    //        }
    //    //        hasRole = IsTopAdmin(user);
    //    //        if (connection != true || hasRole != true)
    //    //        {
    //    //            StatusMessage = "Error: You are not connected to this provider";
    //    //            return (false, false, StatusMessage);
    //    //        }
    //    //        else if (user.IsInRole(UserAccountType.ITAdmin.ToString()) || FebrisAuthorityCheck(user))
    //    //        {
    //    //            isApproved = true;
    //    //        }

    //    //        return (isFebris, isApproved, StatusMessage);
    //    //    }
    //    //    catch (DbUpdateConcurrencyException)
    //    //    {
    //    //        StatusMessage = "Error: An error has occured while processing this request";
    //    //        return (false, false, StatusMessage);
    //    //    }
    //    //}

    //    //public async Task<(bool IsFebris, bool AuthroizedUser, string StatusMessage)> TopClearanceAuthorityCheck(ClaimsPrincipal user, Professional professional, Location location, Institution provider)
    //    //{
    //    //    try
    //    //    {
    //    //        bool isFebris = false;
    //    //        bool isApproved = false;
    //    //        bool userToProviderConnection = false;
    //    //        bool userToLocationConnection = false;
    //    //        bool providerConnectedtoLocation = false;
    //    //        bool hasRole = false;
    //    //        bool userHasAuthority = false;
    //    //        Guid userId = new Guid(_userManager.GetUserId(user));
    //    //        UserActiveAsync(userId);
    //    //        UserPasswordExpiration(userId);
    //    //        if (Guid.Empty == userId)
    //    //        {
    //    //            StatusMessage = "Error: No one is signed in"; // maybe redirect to signin?
    //    //            return (false, false, StatusMessage);
    //    //        }
    //    //        isFebris = IsFebris(user);
    //    //        if (isFebris == true)
    //    //        {
    //    //            return (true, true, StatusMessage);
    //    //        }
    //    //        userToProviderConnection = IsProviderConnectedToUser(provider, userId);
    //    //        if (userToProviderConnection != true)
    //    //        {
    //    //            StatusMessage = "Error: You are not connected to this provider";
    //    //            return (false, false, StatusMessage);
    //    //        }
    //    //        userToLocationConnection = await _context.LocationLinkedUser
    //    //            .Where(l => l.Location == location && l.UserId == userId)
    //    //            .AnyAsync();
    //    //        if (userToProviderConnection != true)
    //    //        {
    //    //            providerConnectedtoLocation = await _context.InstitutionLinkedLocation
    //    //                .Where(p => p.Institution == provider && p.Location == location)
    //    //                .AnyAsync();
    //    //        }

    //    //        if (userToLocationConnection == false && providerConnectedtoLocation == false)
    //    //        {
    //    //            StatusMessage = "Error: You are not connected to this location";
    //    //            return (false, false, StatusMessage);
    //    //        }
    //    //        userHasAuthority = UserHasAuthority(user, professional);
    //    //        hasRole = IsTopAdmin(user);
    //    //        if (userHasAuthority != true || hasRole != true)
    //    //        {
    //    //            StatusMessage = "You do not have authority to modify, add, or remove this professional";
    //    //            return (false, false, StatusMessage);
    //    //        }
    //    //        else if (userHasAuthority == true || FebrisAuthorityCheck(user))
    //    //        {
    //    //            isApproved = true;
    //    //        }
    //    //        return (isFebris, isApproved, StatusMessage);
    //    //    }
    //    //    catch (DbUpdateConcurrencyException)
    //    //    {
    //    //        StatusMessage = "Error: An error has occured while processing this request";
    //    //        return (false, false, StatusMessage);
    //    //    }
    //    //}

    //    ////***********************************************************************************************************************************
    //    ////multi-permission check 
    //    ////***********************************************************************************************************************************
    //    //public async Task<(bool IsFebris, bool AuthroizedUser, string StatusMessage)> TopClearanceAuthorityCheck(ClaimsPrincipal user, Professional professional, Institution provider)
    //    //{
    //    //    try
    //    //    {
    //    //        bool isFebris = false;
    //    //        bool isApproved = false;
    //    //        bool connection = false;
    //    //        bool userHasAuthority = false;
    //    //        bool hasRole = false;
    //    //        Guid userId = new Guid(_userManager.GetUserId(user));
    //    //        UserActiveAsync(userId);
    //    //        UserPasswordExpiration(userId);
    //    //        if (Guid.Empty == userId)
    //    //        {
    //    //            StatusMessage = "Error: No one is signed in";
    //    //            return (false, false, StatusMessage);
    //    //        }
    //    //        isFebris = IsFebris(user);
    //    //        if (isFebris == true)
    //    //        {
    //    //            return (true, true, StatusMessage);
    //    //        }
    //    //        connection = await _context.InstitutionLinkedUser.Where(p => p.Institution.Id == provider.Id && p.UserId == userId).AnyAsync();
    //    //        if (connection != true)
    //    //        {
    //    //            StatusMessage = "Error: You are not connected to this provider";
    //    //            return (false, false, StatusMessage);
    //    //        }
    //    //        userHasAuthority = UserHasAuthority(user, professional);
    //    //        hasRole = IsTopAdmin(user);
    //    //        if (userHasAuthority != true || hasRole != true)
    //    //        {
    //    //            StatusMessage = "You do not have authority to modify, add, or remove this professional";
    //    //            return (false, false, StatusMessage);
    //    //        }
    //    //        else if (userHasAuthority == true || FebrisAuthorityCheck(user))
    //    //        {
    //    //            isApproved = true;
    //    //        }
    //    //        return (isFebris, isApproved, StatusMessage);
    //    //    }
    //    //    catch (DbUpdateConcurrencyException)
    //    //    {
    //    //        StatusMessage = "Error: An error has occured while processing this request";
    //    //        return (false, false, StatusMessage);
    //    //    }
    //    //}


    //    #endregion

    //    #region Febris Exclusive Authority check
    //    ////***********************************************************************************************************************************
    //    ////Febris role checking
    //    ////***********************************************************************************************************************************        
    //    //public bool FebrisAuthorityCheck(ClaimsPrincipal user)
    //    //{
    //    //    Guid userId = new Guid(_userManager.GetUserId(user));
    //    //    if (Guid.Empty == userId)
    //    //    {
    //    //        return false;
    //    //    }
    //    //    UserActiveAsync(userId);
    //    //    UserPasswordExpiration(userId);
    //    //    if (IsFebris(user))
    //    //    {
    //    //        return true;
    //    //    }
    //    //    return false;
    //    //}

    //    #endregion

    //    #region Febris Exclusive High Clearance Authority Check
    //    ////***********************************************************************************************************************************
    //    ////check permissions for febris
    //    ////***********************************************************************************************************************************
    //    //public bool FebrisAdminPermissions(ClaimsPrincipal user)
    //    //{
    //    //    //var userId = _userManager.GetUserId(user);

    //    //    //if (String.IsNullOrEmpty(userId))
    //    //    Guid userId = new Guid(_userManager.GetUserId(user));


    //    //    if (Guid.Empty == userId)
    //    //    {
    //    //        return false;
    //    //    }

    //    //    UserActiveAsync(userId);
    //    //    UserPasswordExpiration(userId);
    //    //    //Also connect to Roles            
    //    //    if (IsFebrisAdmin(user))
    //    //    {
    //    //        return true;
    //    //    }
    //    //    else
    //    //    {
    //    //        return false;
    //    //    }
    //    //}
    //    #endregion

    //    #region Highest Febris Authority Check
    //    //public bool TopFebrisAdminPermissions(ClaimsPrincipal user)
    //    //{
    //    //    //var userId = _userManager.GetUserId(user);

    //    //    //if (String.IsNullOrEmpty(userId))
    //    //    Guid userId = new Guid(_userManager.GetUserId(user));


    //    //    if (Guid.Empty == userId)
    //    //    {
    //    //        return false;
    //    //    }

    //    //    UserActiveAsync(userId);
    //    //    UserPasswordExpiration(userId);
    //    //    //Also connect to Roles            
    //    //    if (IsSystemAdmin(user))
    //    //    {
    //    //        return true;
    //    //    }
    //    //    else
    //    //    {
    //    //        return false;
    //    //    }
    //    //}
    //    #endregion

    //    #region Personal Authority check
    //    //***********************************************************************************************************************************
    //    //Febris role checking
    //    //***********************************************************************************************************************************        
    //    //public bool PersonalAuthorityCheck(ClaimsPrincipal user, Professional professional)
    //    //{
    //    //    bool isFebris = false;
    //    //    bool isAuthorized = false;
    //    //    bool isTheirOwn = false;
    //    //    Guid userId = new Guid(_userManager.GetUserId(user));
    //    //    if (Guid.Empty == userId)
    //    //    {
    //    //        return false;
    //    //    }
    //    //    UserActiveAsync(userId);
    //    //    UserPasswordExpiration(userId);
    //    //    //check if has higher authority 
    //    //    //then check if user has their own authroity
    //    //    isFebris = FebrisAuthorityCheck(user);
    //    //    isAuthorized = UserHasAuthority(user, professional);
    //    //    isTheirOwn = UserConnectedToProfessional(user, professional);
    //    //    if (isFebris || isAuthorized || isTheirOwn)
    //    //    {
    //    //        return true;
    //    //    }
    //    //    return false;
    //    //}

    //    //public bool PersonalStatementAuthorityCheck(ClaimsPrincipal user, Professional professional)
    //    //{
    //    //    bool isFebris = false;
    //    //    bool isAuthorized = false;
    //    //    bool isTheirOwn = false;
    //    //    Guid userId = new Guid(_userManager.GetUserId(user));
    //    //    if (Guid.Empty == userId)
    //    //    {
    //    //        return false;
    //    //    }
    //    //    UserActiveAsync(userId);
    //    //    UserPasswordExpiration(userId);
    //    //    //check if has higher authority 
    //    //    //then check if user has their own authroity
    //    //    isFebris = FebrisAuthorityCheck(user);
    //    //    isAuthorized = UserConnectedtoConnectedProvider(userId, professional);
    //    //    isTheirOwn = UserConnectedToProfessional(user, professional);
    //    //    if (isFebris || isAuthorized || isTheirOwn)
    //    //    {
    //    //        return true;
    //    //    }
    //    //    return false;
    //    //}

    //    //public bool PersonalStatementAuthorityCheck(ClaimsPrincipal user, Statement statement)
    //    //{
    //    //    bool isFebris = false;
    //    //    bool isAuthorized = false;
    //    //    bool isTheirOwn = false;
    //    //    Guid userId = new Guid(_userManager.GetUserId(user));
    //    //    if (Guid.Empty == userId)
    //    //    {
    //    //        return false;
    //    //    }
    //    //    UserActiveAsync(userId);
    //    //    UserPasswordExpiration(userId);
    //    //    //check if has higher authority 
    //    //    //then check if user has their own authroity
    //    //    isFebris = FebrisAuthorityCheck(user);
    //    //    //get actor and check actor against professionl
    //    //    isAuthorized = UserConnectedtoConnectedProvider(userId, professional);
    //    //    isTheirOwn = UserConnectedToProfessional(user, professional);
    //    //    if (isFebris || isAuthorized || isTheirOwn)
    //    //    {
    //    //        return true;
    //    //    }
    //    //    return false;
    //    //}

    //    #endregion

    //    #region Role checks
    //    ////***********************************************************************************************************************************
    //    ////User connected to location 
    //    ////***********************************************************************************************************************************
    //    //private bool IsFebris(ClaimsPrincipal user)
    //    //{
    //    //    try
    //    //    {
    //    //        if (user.IsInRole(FebrisUserType.FebrisEmployee.ToString())
    //    //            || user.IsInRole(FebrisUserType.SystemAdmin.ToString())
    //    //            || user.IsInRole(FebrisUserType.SuperAdmin.ToString()))
    //    //        {
    //    //            return true;
    //    //        }
    //    //        return false;
    //    //    }
    //    //    catch
    //    //    {
    //    //        return false;
    //    //    }
    //    //}
    //    ////***********************************************************************************************************************************
    //    ////User connected to location 
    //    ////***********************************************************************************************************************************
    //    //private bool IsFebrisAdmin(ClaimsPrincipal user)
    //    //{
    //    //    try
    //    //    {
    //    //        if (user.IsInRole(FebrisUserType.SystemAdmin.ToString())
    //    //            || user.IsInRole(FebrisUserType.SuperAdmin.ToString()))
    //    //        {
    //    //            return true;
    //    //        }
    //    //        return false;
    //    //    }
    //    //    catch
    //    //    {
    //    //        return false;
    //    //    }
    //    //}
    //    ////***********************************************************************************************************************************
    //    ////User connected to location 
    //    ////***********************************************************************************************************************************
    //    //private bool IsSystemAdmin(ClaimsPrincipal user)
    //    //{
    //    //    try
    //    //    {
    //    //        if (user.IsInRole(FebrisUserType.SystemAdmin.ToString()))
    //    //        {
    //    //            return true;
    //    //        }
    //    //        return false;
    //    //    }
    //    //    catch
    //    //    {
    //    //        return false;
    //    //    }
    //    //}
    //    ////***********************************************************************************************************************************
    //    ////User connected to location 
    //    ////***********************************************************************************************************************************
    //    //private bool IsUser(ClaimsPrincipal user)
    //    //{
    //    //    try
    //    //    {
    //    //        if (user.IsInRole(UserAccountType.Student.ToString())
    //    //            || user.IsInRole(UserAccountType.ITAdmin.ToString())
    //    //            || user.IsInRole(UserAccountType.Legal.ToString())
    //    //            || user.IsInRole(UserAccountType.Educator.ToString())
    //    //            || user.IsInRole(UserAccountType.Supervisor.ToString())
    //    //            || user.IsInRole(UserAccountType.Executive.ToString())
    //    //            || user.IsInRole(FebrisUserType.FebrisEmployee.ToString())
    //    //            || user.IsInRole(FebrisUserType.SystemAdmin.ToString())
    //    //            || user.IsInRole(FebrisUserType.SuperAdmin.ToString()))
    //    //        {
    //    //            return true;
    //    //        }
    //    //        return false;
    //    //    }
    //    //    catch
    //    //    {
    //    //        return false;
    //    //    }
    //    //}
    //    ////***********************************************************************************************************************************
    //    ////User connected to location 
    //    ////***********************************************************************************************************************************
    //    //private bool IsAdmin(ClaimsPrincipal user)
    //    //{
    //    //    try
    //    //    {
    //    //        if (user.IsInRole(UserAccountType.ITAdmin.ToString())
    //    //            || user.IsInRole(UserAccountType.Legal.ToString())
    //    //            || user.IsInRole(UserAccountType.Educator.ToString())
    //    //            || user.IsInRole(UserAccountType.Supervisor.ToString())
    //    //            || user.IsInRole(UserAccountType.Executive.ToString())
    //    //            || user.IsInRole(FebrisUserType.FebrisEmployee.ToString())
    //    //            || user.IsInRole(FebrisUserType.SystemAdmin.ToString())
    //    //            || user.IsInRole(FebrisUserType.SuperAdmin.ToString()))
    //    //        {
    //    //            return true;
    //    //        }
    //    //        return false;
    //    //    }
    //    //    catch
    //    //    {
    //    //        return false;
    //    //    }
    //    //}
    //    ////***********************************************************************************************************************************
    //    ////User connected to location 
    //    ////***********************************************************************************************************************************
    //    //private bool IsHigherAdmin(ClaimsPrincipal user)
    //    //{
    //    //    try
    //    //    {
    //    //        if (user.IsInRole(UserAccountType.ITAdmin.ToString())
    //    //            || user.IsInRole(UserAccountType.Legal.ToString())
    //    //            || user.IsInRole(UserAccountType.Educator.ToString())
    //    //            || user.IsInRole(UserAccountType.Supervisor.ToString())
    //    //            || user.IsInRole(UserAccountType.Executive.ToString())
    //    //            || user.IsInRole(FebrisUserType.FebrisEmployee.ToString())
    //    //            || user.IsInRole(FebrisUserType.SystemAdmin.ToString())
    //    //            || user.IsInRole(FebrisUserType.SuperAdmin.ToString()))
    //    //        {
    //    //            return true;
    //    //        }
    //    //        return false;
    //    //    }
    //    //    catch
    //    //    {
    //    //        return false;
    //    //    }
    //    //}
    //    ////***********************************************************************************************************************************
    //    ////User connected to location 
    //    ////***********************************************************************************************************************************
    //    //private bool IsTopAdmin(ClaimsPrincipal user)
    //    //{
    //    //    try
    //    //    {
    //    //        if (user.IsInRole(UserAccountType.ITAdmin.ToString())
    //    //            || user.IsInRole(UserAccountType.Executive.ToString())
    //    //            || user.IsInRole(FebrisUserType.FebrisEmployee.ToString())
    //    //            || user.IsInRole(FebrisUserType.SystemAdmin.ToString())
    //    //            || user.IsInRole(FebrisUserType.SuperAdmin.ToString()))
    //    //        {
    //    //            return true;
    //    //        }
    //    //        return false;
    //    //    }
    //    //    catch
    //    //    {
    //    //        return false;
    //    //    }
    //    //}


    //    #endregion

    //    #region "General"


    //    //***********************************************************************************************************************************
    //    //internal use - change from public
    //    //***********************************************************************************************************************************
    //    //public bool IsProviderConnectedToUser(Institution provider, Guid userId)
    //    //{
    //    //    try
    //    //    {
    //    //        return _context.InstitutionLinkedUser.Where(p => p.Institution.Id == provider.Id && p.UserId == userId).Any();
    //    //    }
    //    //    catch (DbUpdateConcurrencyException)
    //    //    {
    //    //        return false;
    //    //    }
    //    //}

    //    ////***********************************************************************************************************************************
    //    ////internal use - change from public
    //    ////***********************************************************************************************************************************
    //    //private bool UserHasAuthority(ClaimsPrincipal user, Professional professional)
    //    //{
    //    //    try
    //    //    {
    //    //        if (user.IsInRole(UserAccountType.ITAdmin.ToString()))
    //    //        {
    //    //            if (professional.UserAccountType.ToString() == UserAccountType.Student.ToString()
    //    //               || professional.UserAccountType.ToString() == UserAccountType.Educator.ToString()
    //    //               || professional.UserAccountType.ToString() == UserAccountType.Supervisor.ToString()
    //    //               || professional.UserAccountType.ToString() == UserAccountType.Executive.ToString()
    //    //               || professional.UserAccountType.ToString() == UserAccountType.Legal.ToString()
    //    //               || professional.UserAccountType.ToString() == UserAccountType.ITAdmin.ToString())
    //    //            {
    //    //                return true;
    //    //            }
    //    //        }
    //    //        else if (user.IsInRole(UserAccountType.Supervisor.ToString()))
    //    //        {
    //    //            if (professional.UserAccountType.ToString() == UserAccountType.Student.ToString()
    //    //               || professional.UserAccountType.ToString() == UserAccountType.Educator.ToString()
    //    //               || professional.UserAccountType.ToString() == UserAccountType.Supervisor.ToString())
    //    //            {
    //    //                return true;
    //    //            }
    //    //        }
    //    //        else if (user.IsInRole(UserAccountType.Executive.ToString()))
    //    //        {
    //    //            if (professional.UserAccountType.ToString() == UserAccountType.Student.ToString()
    //    //               || professional.UserAccountType.ToString() == UserAccountType.Educator.ToString()
    //    //               || professional.UserAccountType.ToString() == UserAccountType.Supervisor.ToString()
    //    //               || professional.UserAccountType.ToString() == UserAccountType.Executive.ToString()
    //    //               || professional.UserAccountType.ToString() == UserAccountType.Legal.ToString()
    //    //               || professional.UserAccountType.ToString() == UserAccountType.ITAdmin.ToString())
    //    //            {
    //    //                return true;
    //    //            }
    //    //        }
    //    //        else if (user.IsInRole(UserAccountType.Educator.ToString()))
    //    //        {
    //    //            if (professional.UserAccountType.ToString() == UserAccountType.Student.ToString()
    //    //               || professional.UserAccountType.ToString() == UserAccountType.Educator.ToString()
    //    //               )
    //    //            {
    //    //                return true;
    //    //            }
    //    //        }
    //    //        return false;
    //    //    }
    //    //    catch (DbUpdateConcurrencyException)
    //    //    {
    //    //        return false;
    //    //    }
    //    //}

    //    //***********************************************************************************************************************************
    //    //make sure no strange characters can be used
    //    //***********************************************************************************************************************************
    //    //public bool CharacterCheck(string inputString)
    //    //{
    //    //    string[] unacceptableCharacters = new string[] { "\"", "!", "/", @"\", "|", "?", "@", "$", "%", "^", "&", "*", "(", ")", "_", "-", "+", "=", "~", "`", ";", ":", ".", "'", "{", "}", "[", "]" };
    //    //    if (inputString == null) { }
    //    //    else if (unacceptableCharacters.Any(s => s.Contains(inputString)) == true)
    //    //    {
    //    //        return false;
    //    //    }
    //    //    return true;
    //    //}


    //    ////***********************************************************************************************************************************
    //    //// user is connected to professional
    //    ////***********************************************************************************************************************************
    //    //public bool UserConnectedToProfessional(ClaimsPrincipal user, Professional professional)
    //    //{
    //    //    try
    //    //    {
    //    //        Guid userId = new Guid(_userManager.GetUserId(user));
    //    //        bool isConnected = _context.ProfessionalLinkedUser
    //    //            .Where(i => i.UserId == userId && i.Professional == professional)
    //    //            .Any();

    //    //        return isConnected;
    //    //    }
    //    //    catch (Exception)
    //    //    {

    //    //        return false;
    //    //    }
    //    //}

    //    ////***********************************************************************************************************************************
    //    ////checking if user is connected to any provider for switchboard routing
    //    ////***********************************************************************************************************************************
    //    //public bool UserConnectedToAnyProvider(Guid userId)
    //    //{
    //    //    try
    //    //    {
    //    //        return _context.InstitutionLinkedUser.Where(p => p.UserId == userId).Any();
    //    //    }
    //    //    catch (DbUpdateConcurrencyException)
    //    //    {
    //    //        return false;
    //    //    }
    //    //}

    //    ////***********************************************************************************************************************************
    //    ////checking if user is connected to any provider for switchboard routing
    //    ////***********************************************************************************************************************************
    //    //public bool UserConnectedtoAnyEducationOrganization(Guid userId)
    //    //{
    //    //    try
    //    //    {
    //    //        return _context.ModuleDeveloperUser.Where(p => p.UserId == userId).Any();
    //    //    }
    //    //    catch (DbUpdateConcurrencyException)
    //    //    {
    //    //        return false;
    //    //    }
    //    //}

    //    ////***********************************************************************************************************************************
    //    //// 
    //    ////***********************************************************************************************************************************
    //    ////***********************************************************************************************************************************
    //    ////internal use - change from public
    //    ////***********************************************************************************************************************************
    //    //public async Task<bool> FebrisUserHasAuthority(ClaimsPrincipal user, ApplicationUser targetUser, FebrisUserType occupation)
    //    //{
    //    //    string targetUserRole = string.Empty;
    //    //    if (targetUser != null)
    //    //    {
    //    //        targetUserRole = String.Join(",", await _userManager.GetRolesAsync(targetUser));
    //    //    }
    //    //    else
    //    //    {
    //    //        targetUserRole = occupation.ToString();
    //    //    }

    //    //    try
    //    //    {
    //    //        if (user.IsInRole(FebrisUserType.SystemAdmin.ToString()))
    //    //        {
    //    //            return true;
    //    //            //if (targetUserRole == FebrisUserType.FebrisEmployee.ToString()
    //    //            //   || targetUserRole == FebrisUserType.SuperAdmin.ToString()
    //    //            //   || targetUserRole == FebrisUserType.SystemAdmin.ToString()
    //    //            //   || targetUserRole == EducationOrganizationOccupationType.EduITAdmin.ToString()
    //    //            //   || targetUserRole == EducationOrganizationOccupationType.EduEmployee.ToString())
    //    //            //{
    //    //            //    return true;
    //    //            //}
    //    //        }
    //    //        else if (user.IsInRole(FebrisUserType.SuperAdmin.ToString()))
    //    //        {
    //    //            if (targetUserRole != FebrisUserType.SystemAdmin.ToString())
    //    //            {
    //    //                return true;
    //    //            }
    //    //            //if (targetUserRole == FebrisUserType.FebrisEmployee.ToString()
    //    //            //   || targetUserRole == FebrisUserType.SuperAdmin.ToString() 
    //    //            //   || targetUserRole == EducationOrganizationOccupationType.EduITAdmin.ToString()
    //    //            //   || targetUserRole == EducationOrganizationOccupationType.EduEmployee.ToString())
    //    //            //{
    //    //            //    return true;
    //    //            //}
    //    //        }

    //    //        return false;
    //    //    }
    //    //    catch (DbUpdateConcurrencyException)
    //    //    {
    //    //        return false;
    //    //    }
    //    //}

    //    ////***********************************************************************************************************************************
    //    ////user is connected to provider connected to this statement
    //    ////***********************************************************************************************************************************
    //    //public bool UserConnectedtoConnectedProvider(Guid userId, Professional professional)
    //    //{
    //    //    try
    //    //    {
    //    //        List<Institution> providerList = _context
    //    //            .InstitutionLinkedUser
    //    //            .Include(p => p.Institution)
    //    //            .Where(u => u.UserId == userId)
    //    //            .Select(p => p.Institution)
    //    //            .ToList();

    //    //        bool isAuthorized = _context.InstitutionLinkedProfessional
    //    //            .Where(u => providerList.Any(x => x == u.Institution)
    //    //            && u.Professional == professional
    //    //            && u.AttachmentStatus == AttachmentStatus.Attached)
    //    //            .Any();

    //    //        return isAuthorized;
    //    //    }
    //    //    catch (DbUpdateConcurrencyException)
    //    //    {
    //    //        return false;
    //    //    }
    //    //}


    //    ////***********************************************************************************************************************************
    //    ////checking User Activity
    //    ////***********************************************************************************************************************************
    //    //private bool UserActiveAsync(Guid id)
    //    //{
    //    //    try
    //    //    {
    //    //        try
    //    //        {
    //    //            //pull date of password set
    //    //            UserSecurityAndTracking userfromdb = _applicationUser.UserSecurityAndTracking.FirstOrDefault(u => u.UserId == id);
    //    //            userfromdb.LastLoginTime = DateTime.UtcNow;
    //    //            _applicationUser.SaveChanges();
    //    //            return true;
    //    //        }
    //    //        catch
    //    //        {
    //    //            _applicationUser.UserSecurityAndTracking.Add(new UserSecurityAndTracking()
    //    //            {
    //    //                UserId = id,
    //    //                RegistrationDate = DateTime.UtcNow,
    //    //                LastLoginTime = DateTime.UtcNow,
    //    //                PasswordSet = DateTime.UtcNow
    //    //            });
    //    //            _applicationUser.SaveChanges();
    //    //            return true;
    //    //        }
    //    //    }
    //    //    catch
    //    //    {
    //    //        return false;
    //    //    }
    //    //}

    //    #endregion

    //    #region Password handling
    //    //***********************************************************************************************************************************
    //    //randomizing Identification number
    //    //***********************************************************************************************************************************        
    //    //public string RandomeString(string occupation)
    //    //{

    //    //    int length = 10;
    //    //    string randomString = new string(Enumerable.Repeat(chars, length).Select(s => s[random.Next(s.Length)]).ToArray());
    //    //    string identString = (occupation + "_" + randomString);
    //    //    if (true == _context.Professional.Any(i => i.IdentificationNumber == identString))
    //    //    {
    //    //        //not sure if this works.
    //    //        return RandomeString(occupation);
    //    //    }
    //    //    return identString;
    //    //}

    //    //***********************************************************************************************************************************
    //    //password randomizer
    //    //private static RNGCryptoServiceProvider rngCsp = new RNGCryptoServiceProvider();
    //    //const string passwordChars = "qwertyuiopasdfghjklzxcvbnmQWERTYUIOPASDFGHJKLZXCVBNM0123456789!@#$%^&*()_-?,.*+~`";
    //    //***********************************************************************************************************************************
    //    public string PasswordRandomize()
    //    {
    //        var opts = new PasswordOptions()
    //        {
    //            RequiredLength = 20,
    //            RequiredUniqueChars = 4,
    //            RequireDigit = true,
    //            RequireLowercase = true,
    //            RequireNonAlphanumeric = true,
    //            RequireUppercase = true
    //        };

    //        string[] randomChars = new[] {
    //            "ABCDEFGHJKLMNOPQRSTUVWXYZ",    // uppercase 
    //            "abcdefghijkmnopqrstuvwxyz",    // lowercase
    //            "0123456789",                   // digits
    //            "!@$?_-"                        // non-alphanumeric
    //        };
    //        Random rand = new Random(Environment.TickCount);
    //        List<char> chars = new List<char>();

    //        if (opts.RequireUppercase)
    //            chars.Insert(rand.Next(0, chars.Count),
    //                randomChars[0][rand.Next(0, randomChars[0].Length)]);

    //        if (opts.RequireLowercase)
    //            chars.Insert(rand.Next(0, chars.Count),
    //                randomChars[1][rand.Next(0, randomChars[1].Length)]);

    //        if (opts.RequireDigit)
    //            chars.Insert(rand.Next(0, chars.Count),
    //                randomChars[2][rand.Next(0, randomChars[2].Length)]);

    //        if (opts.RequireNonAlphanumeric)
    //            chars.Insert(rand.Next(0, chars.Count),
    //                randomChars[3][rand.Next(0, randomChars[3].Length)]);

    //        for (int i = chars.Count; i < opts.RequiredLength
    //            || chars.Distinct().Count() < opts.RequiredUniqueChars; i++)
    //        {
    //            string rcs = randomChars[rand.Next(0, randomChars.Length)];
    //            chars.Insert(rand.Next(0, chars.Count),
    //                rcs[rand.Next(0, rcs.Length)]);
    //        }

    //        return new string(chars.ToArray());


    //    }

    //    //***********************************************************************************************************************************
    //    //change password every 3 months
    //    //***********************************************************************************************************************************
    //    //public bool UserPasswordExpiration(Guid id)
    //    //{
    //    //    try
    //    //    {
    //    //        //pull date of password set
    //    //        UserSecurityAndTracking userfromdb = _applicationUser.UserSecurityAndTracking.First(u => u.UserId == id);
    //    //        //check if it has been the password set it less than 3 months
    //    //        if (userfromdb.PasswordSet < DateTime.UtcNow.AddMonths(-3))
    //    //        {
    //    //            StatusMessage = "You need to create a new password";
    //    //            return false;
    //    //        }
    //    //        else
    //    //        {
    //    //            //start letting them know to change password in the next 10 days
    //    //            if (userfromdb.PasswordSet < DateTime.UtcNow.AddDays(-75))
    //    //            {
    //    //                var daysLeft = (userfromdb.PasswordSet - DateTime.UtcNow.AddMonths(-3)).TotalDays;
    //    //                StatusMessage = "Need to change your password in the next " + daysLeft + " days";
    //    //            }
    //    //            return true;

    //    //        }
    //    //    }
    //    //    catch
    //    //    {
    //    //        return true; // this needs to be changed 
    //    //    }

    //    //}
    //    ////***********************************************************************************************************************************
    //    ////resetting password
    //    ////***********************************************************************************************************************************
    //    //public bool PasswordResetCounter(ClaimsPrincipal user)//can use userID string to set this if it goes to set password and change password Reset password?
    //    //{
    //    //    try
    //    //    {
    //    //        var reset = _applicationUser.UserSecurityAndTracking.Find(user);
    //    //        reset.PasswordSet = DateTime.UtcNow;
    //    //        _applicationUser.SaveChanges();
    //    //    }
    //    //    catch
    //    //    {
    //    //        StatusMessage = "The 3 Month peiriod was not reset";
    //    //    }
    //    //    //reset password countdown            

    //    //    return true;
    //    //}




    //    #endregion




    //    //***********************************************************************************************************************************
    //    //check image uploads to ensure nothing fishy is going on

    //    //***********************************************************************************************************************************
    //    //check DDos attack

    //    //***********************************************************************************************************************************

    //    //mark actions of specific users?

    //    //***********************************************************************************************************************************
    //    //test search queries to protect sql commands

    //}
}
